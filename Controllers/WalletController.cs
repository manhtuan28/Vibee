using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;
using Vibee.Data;
using Vibee.Models;
using Vibee.Services;

namespace Vibee.Controllers
{
    [Authorize]
    public class WalletController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public WalletController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
        }
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                await _signInManager.SignOutAsync();
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.UserId == user.Id);

            if (wallet == null)
            {
                wallet = new Wallet { UserId = user.Id, Balance = 0, Currency = "VND" };
                _context.Wallets.Add(wallet);
                await _context.SaveChangesAsync();
            }

            var transactions = await _context.Transactions
                .Include(t => t.SenderWallet).ThenInclude(w => w.User)
                .Include(t => t.ReceiverWallet).ThenInclude(w => w.User)
                .Where(t => t.SenderWalletId == wallet.Id || t.ReceiverWalletId == wallet.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Take(20)
                .ToListAsync();

            ViewBag.Transactions = transactions;
            return View(wallet);
        }

        [HttpGet]
        public IActionResult Transfer()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Transfer(string receiverEmail, decimal amount, string message)
        {
            if (amount < 1000)
            {
                ModelState.AddModelError("", "Số tiền chuyển tối thiểu là 1,000 VND.");
                return View();
            }

            var senderUser = await _userManager.GetUserAsync(User);
            if (senderUser == null)
            {
                await _signInManager.SignOutAsync();
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            if (senderUser.Email.ToLower() == receiverEmail.ToLower())
            {
                ModelState.AddModelError("", "Không thể tự chuyển tiền cho chính mình.");
                return View();
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var senderWallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == senderUser.Id);
                if (senderWallet == null || senderWallet.Balance < amount)
                {
                    ModelState.AddModelError("", "Số dư không đủ để thực hiện giao dịch.");
                    return View();
                }

                var receiverUser = await _context.Users.Include(u => u.Wallet).FirstOrDefaultAsync(u => u.Email == receiverEmail);
                if (receiverUser == null)
                {
                    ModelState.AddModelError("", "Không tìm thấy người dùng với Email này.");
                    return View();
                }

                if (receiverUser.Wallet == null)
                {
                    receiverUser.Wallet = new Wallet { UserId = receiverUser.Id, Balance = 0 };
                    _context.Wallets.Add(receiverUser.Wallet);
                    await _context.SaveChangesAsync();
                }

                senderWallet.Balance -= amount;
                senderWallet.LastUpdated = DateTime.Now;

                receiverUser.Wallet.Balance += amount;
                receiverUser.Wallet.LastUpdated = DateTime.Now;

                var history = new Transaction
                {
                    Id = Guid.NewGuid(),
                    SenderWalletId = senderWallet.Id,
                    ReceiverWalletId = receiverUser.Wallet.Id,
                    Amount = amount,
                    Message = message,
                    Type = TransactionType.Transfer,
                    Status = TransactionStatus.Completed,
                    CreatedAt = DateTime.Now
                };

                _context.Transactions.Add(history);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                TempData["SuccessMsg"] = $"Đã chuyển thành công {amount:N0} VND cho {receiverUser.FullName}.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError("", "Có lỗi xảy ra: " + ex.Message);
                return View();
            }
        }

        [HttpGet]
        public IActionResult Deposit()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateDeposit(decimal amount, string gateway)
        {
            if (amount < 10000) return BadRequest("Số tiền nạp tối thiểu là 10,000 VND");

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                await _signInManager.SignOutAsync();
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == user.Id);
            if (wallet == null) return NotFound("Chưa có ví");

            var transId = Guid.NewGuid();

            var transaction = new Transaction
            {
                Id = transId,
                SenderWalletId = null,
                ReceiverWalletId = wallet.Id,
                Amount = amount,
                Type = TransactionType.Deposit,
                Status = TransactionStatus.Pending,
                Message = $"Nạp tiền qua {gateway}",
                CreatedAt = DateTime.Now
            };
            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            if (gateway == "VNPAY")
            {
                return Redirect(GetVnPayUrl(amount, transId.ToString()));
            }
            else if (gateway == "MOMO")
            {
                var response = await GetMomoUrl(amount, transId.ToString());

                if (response == null || string.IsNullOrEmpty(response.PayUrl))
                {
                    transaction.Status = TransactionStatus.Failed;
                    transaction.Message += " - Lỗi: " + (response?.Message ?? "Unknown");
                    await _context.SaveChangesAsync();

                    return Content($"Lỗi từ MoMo: {response?.Message} (Code: {response?.ErrorCode})");
                }

                return Redirect(response.PayUrl);
            }

            return BadRequest("Cổng thanh toán không hợp lệ");
        }

        [HttpGet]
        public async Task<IActionResult> PaymentCallback()
        {
            var query = HttpContext.Request.Query;
            Console.WriteLine("CALLBACK URL: " + HttpContext.Request.GetDisplayUrl());

            bool isSuccess = false;
            string orderId = "";
            string gateway = "";

            if (query.ContainsKey("vnp_SecureHash"))
            {
                gateway = "VNPAY";
                var vnpay = new VnPayLibrary();
                foreach (var (key, value) in query)
                {
                    if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                        vnpay.AddResponseData(key, value.ToString());
                }

                string vnp_HashSecret = Environment.GetEnvironmentVariable("VNPAY_HASH_SECRET") ?? string.Empty;

                if (vnpay.ValidateSignature(query["vnp_SecureHash"], vnp_HashSecret) && query["vnp_ResponseCode"] == "00")
                {
                    isSuccess = true;
                    orderId = query["vnp_TxnRef"];
                }
            }
            else if (query.ContainsKey("partnerCode"))
            {
                gateway = "MOMO";
                string resultCode = query["resultCode"].ToString();
                string errorCode = query["errorCode"].ToString();

                if (resultCode == "0" || errorCode == "0")
                {
                    isSuccess = true;
                    orderId = query["orderId"];
                }
            }

            if (!string.IsNullOrEmpty(orderId))
            {
                if (orderId.Contains("_"))
                {
                    orderId = orderId.Split('_')[0];
                }

                if (Guid.TryParse(orderId, out var transGuid))
                {
                    var transaction = await _context.Transactions
                                            .Include(t => t.ReceiverWallet)
                                            .FirstOrDefaultAsync(t => t.Id == transGuid);

                    if (transaction != null && transaction.Status == TransactionStatus.Pending)
                    {
                        if (isSuccess)
                        {
                            transaction.Status = TransactionStatus.Completed;

                            transaction.ReceiverWallet.Balance += transaction.Amount;
                            transaction.ReceiverWallet.LastUpdated = DateTime.Now;

                            TempData["SuccessMsg"] = $"Nạp thành công {transaction.Amount:N0} đ qua {gateway}.";
                        }
                        else
                        {
                            transaction.Status = TransactionStatus.Failed;
                            transaction.Message += " (Thanh toán thất bại)";
                            TempData["ErrorMsg"] = "Giao dịch thanh toán thất bại.";
                        }

                        await _context.SaveChangesAsync();
                    }
                }
            }

            return RedirectToAction("Index");
        }

        private string GetVnPayUrl(decimal amount, string orderId)
        {
            string vnp_TmnCode = Environment.GetEnvironmentVariable("VNPAY_TMN_CODE") ?? string.Empty;
            string vnp_HashSecret = Environment.GetEnvironmentVariable("VNPAY_HASH_SECRET") ?? string.Empty;
            string vnp_Url = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
            string vnp_Returnurl = Url.Action("PaymentCallback", "Wallet", null, Request.Scheme);

            string vnp_IpAddr = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            if (vnp_IpAddr == "::1") vnp_IpAddr = "127.0.0.1";

            VnPayLibrary vnpay = new VnPayLibrary();
            vnpay.AddRequestData("vnp_Version", "2.1.0");
            vnpay.AddRequestData("vnp_Command", "pay");
            vnpay.AddRequestData("vnp_TmnCode", vnp_TmnCode);

            vnpay.AddRequestData("vnp_Amount", ((long)(amount * 100)).ToString());

            vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            vnpay.AddRequestData("vnp_CurrCode", "VND");
            vnpay.AddRequestData("vnp_IpAddr", vnp_IpAddr);
            vnpay.AddRequestData("vnp_Locale", "vn");

            vnpay.AddRequestData("vnp_OrderInfo", "Nap tien " + orderId);
            vnpay.AddRequestData("vnp_OrderType", "other");
            vnpay.AddRequestData("vnp_ReturnUrl", vnp_Returnurl);
            vnpay.AddRequestData("vnp_TxnRef", orderId);

            return vnpay.CreateRequestUrl(vnp_Url, vnp_HashSecret);
        }

        private async Task<MomoCreatePaymentResponseModel> GetMomoUrl(decimal amount, string orderId)
        {
            var options = new MomoOptionModel();

            string endpoint = options.MomoApiUrl;
            string redirectUrl = Url.Action("PaymentCallback", "Wallet", null, Request.Scheme);
            string ipnUrl = redirectUrl;
            string requestType = options.RequestType;
            string amountStr = ((long)amount).ToString();
            string orderInfo = "Nap tien Vibee";
            string requestId = Guid.NewGuid().ToString();
            string extraData = "";

            string rawHash = "accessKey=" + options.AccessKey +
                "&amount=" + amountStr +
                "&extraData=" + extraData +
                "&ipnUrl=" + ipnUrl +
                "&orderId=" + orderId +
                "&orderInfo=" + orderInfo +
                "&partnerCode=" + options.PartnerCode +
                "&redirectUrl=" + redirectUrl +
                "&requestId=" + requestId +
                "&requestType=" + requestType;

            string signature = ComputeHmacSha256(rawHash, options.SecretKey);

            var message = new
            {
                partnerCode = options.PartnerCode,
                partnerName = "Vibee Store",
                storeId = "Vibee",
                requestId = requestId,
                amount = amountStr,
                orderId = orderId,
                orderInfo = orderInfo,
                redirectUrl = redirectUrl,
                ipnUrl = ipnUrl,
                lang = "vi",
                extraData = extraData,
                requestType = requestType,
                signature = signature
            };

            using (var client = new HttpClient())
            {
                var response = await client.PostAsJsonAsync(endpoint, message);
                var jsonResponse = await response.Content.ReadAsStringAsync();

                return JsonConvert.DeserializeObject<MomoCreatePaymentResponseModel>(jsonResponse);
            }
        }
        private string ComputeHmacSha256(string message, string secretKey)
        {
            var encoding = new ASCIIEncoding();
            byte[] keyByte = encoding.GetBytes(secretKey);
            byte[] messageBytes = encoding.GetBytes(message);
            using (var hmacsha256 = new HMACSHA256(keyByte))
            {
                byte[] hashmessage = hmacsha256.ComputeHash(messageBytes);
                return BitConverter.ToString(hashmessage).Replace("-", "").ToLower();
            }
        }

        [HttpGet]
        public async Task<IActionResult> Repay(Guid id)
        {
            var trans = await _context.Transactions.FindAsync(id);

            if (trans == null || trans.Status != TransactionStatus.Pending || trans.Type != TransactionType.Deposit)
            {
                return RedirectToAction("Index");
            }
            string uniqueOrderId = $"{trans.Id}_{DateTime.Now.Ticks}";

            string gateway = trans.Message.ToUpper().Contains("MOMO") ? "MOMO" : "VNPAY";

            if (gateway == "VNPAY")
            {
                return Redirect(GetVnPayUrl(trans.Amount, uniqueOrderId));
            }
            else
            {
                var response = await GetMomoUrl(trans.Amount, uniqueOrderId);

                if (response == null || string.IsNullOrEmpty(response.PayUrl))
                {
                    return Content($"Lỗi tạo lại link MoMo: {response?.Message ?? "Unknown Error"}");
                }
                return Redirect(response.PayUrl);
            }
        }
    }
}
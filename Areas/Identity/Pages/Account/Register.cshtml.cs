#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Vibee.Models;

namespace Vibee.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly Vibee.Services.SpamProtectionService _spamService;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            RoleManager<IdentityRole> roleManager,
            Vibee.Services.SpamProtectionService spamService)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _roleManager = roleManager;
            _spamService = spamService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập địa chỉ Email.")]
            [EmailAddress(ErrorMessage = "Định dạng Email không hợp lệ.")]
            [StringLength(50, ErrorMessage = "Email tối đa 50 ký tự để ngăn chặn spam.")]
            [RegularExpression(@"^[a-zA-Z0-9._%+-]+@gmail\.com$", ErrorMessage = "Hệ thống chỉ chấp nhận tài khoản Gmail (@gmail.com).")]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập Họ và tên.")]
            [StringLength(50, ErrorMessage = "Họ tên phải từ {2} đến {1} ký tự.", MinimumLength = 2)]
            [Display(Name = "Họ và tên")]
            public string FullName { get; set; }

            [Required(ErrorMessage = "Vui lòng chọn giới tính.")]
            [Display(Name = "Giới tính")]
            public string Gender { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
            [StringLength(100, ErrorMessage = "{0} phải dài ít nhất {2} ký tự.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Mật khẩu")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Xác nhận mật khẩu")]
            [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
            public string ConfirmPassword { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            if (_spamService.IsBlocked(ipAddress, "Register", out double regWaitTime))
            {
                ModelState.AddModelError(string.Empty, $"IP của bạn đã tạo quá nhiều tài khoản. Vui lòng quay lại sau {regWaitTime} phút.");
                return Page();
            }

            if (ModelState.IsValid)
            {
                var user = CreateUser();

                user.FullName = Input.FullName.Trim();
                user.Gender = Input.Gender;
                user.AvatarUrl = "/images/default-avatar.png";
                user.JoinDate = DateTime.Now;
                user.EmailConfirmed = false;

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Đã tạo tài khoản mới thành công.");

                    _spamService.TrackActivity(ipAddress, "Register");

                    if (!await _roleManager.RoleExistsAsync("User"))
                    {
                        await _roleManager.CreateAsync(new IdentityRole("User"));
                    }
                    await _userManager.AddToRoleAsync(user, "User");

                    var userId = await _userManager.GetUserIdAsync(user);
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                        protocol: Request.Scheme);

                    string emailBody = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                    <h2 style='color: #1877f2;'>Chào mừng bạn đến với Vibee!</h2>
                    <p>Xin chào <strong>{user.FullName}</strong>,</p>
                    <p>Cảm ơn bạn đã đăng ký tài khoản. Để bắt đầu sử dụng, vui lòng xác thực địa chỉ email của bạn bằng cách nhấn vào nút bên dưới:</p>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{HtmlEncoder.Default.Encode(callbackUrl)}' style='background-color: #1877f2; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; font-weight: bold;'>Xác thực tài khoản ngay</a>
                    </div>
                    <p style='color: #777; font-size: 12px;'>Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email này.</p>
                </div>";

                    await _emailSender.SendEmailAsync(Input.Email, "Xác thực tài khoản Vibee", emailBody);

                    return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                }

                foreach (var error in result.Errors)
                {
                    var msg = error.Description;
                    if (error.Code == "DuplicateUserName" || error.Code == "DuplicateEmail")
                        msg = $"Email '{Input.Email}' đã được sử dụng bởi tài khoản khác.";
                    if (error.Code.Contains("Password"))
                        msg = "Mật khẩu chưa đủ mạnh (cần chữ hoa, chữ thường, số và ký tự đặc biệt).";

                    ModelState.AddModelError(string.Empty, msg);
                }
            }

            return Page();
        }

        private ApplicationUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(ApplicationUser)}'. " +
                    $"Ensure that '{nameof(ApplicationUser)}' is not an abstract class and has a parameterless constructor.");
            }
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<ApplicationUser>)_userStore;
        }
    }
}
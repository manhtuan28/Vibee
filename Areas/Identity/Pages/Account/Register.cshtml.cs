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

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập Email.")]
            [EmailAddress(ErrorMessage = "Định dạng Email không hợp lệ.")]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập Họ và tên.")]
            [Display(Name = "Họ và tên")]
            [StringLength(100, ErrorMessage = "{0} phải dài ít nhất {2} và tối đa {1} ký tự.", MinimumLength = 2)]
            public string FullName { get; set; }

            [Required(ErrorMessage = "Vui lòng chọn Giới tính.")]
            [Display(Name = "Giới tính")]
            public string Gender { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập Mật khẩu.")]
            [StringLength(100, ErrorMessage = "{0} phải dài ít nhất {2} ký tự.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Mật khẩu")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Xác nhận mật khẩu")]
            [Compare("Password", ErrorMessage = "Mật khẩu nhập lại không khớp.")]
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

            if (ModelState.IsValid)
            {
                var user = CreateUser();

                user.FullName = Input.FullName;
                user.Gender = Input.Gender;
                user.AvatarUrl = "/images/default-avatar.png";
                user.JoinDate = DateTime.Now;

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Người dùng đã tạo tài khoản mới thành công.");

                    var userId = await _userManager.GetUserIdAsync(user);
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailAsync(Input.Email, "Xác nhận email của bạn",
                        $"Vui lòng xác nhận tài khoản Vibee bằng cách <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>bấm vào đây</a>.");

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                    }
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }
                }

                foreach (var error in result.Errors)
                {
                    var description = error.Description;
                    if (error.Code == "DuplicateUserName") description = $"Email '{Input.Email}' đã được sử dụng.";
                    if (error.Code == "PasswordTooShort") description = "Mật khẩu phải dài ít nhất 6 ký tự.";
                    if (error.Code == "PasswordRequiresNonAlphanumeric") description = "Mật khẩu cần có ít nhất một ký tự đặc biệt.";
                    if (error.Code == "PasswordRequiresDigit") description = "Mật khẩu cần có ít nhất một chữ số ('0'-'9').";
                    if (error.Code == "PasswordRequiresUpper") description = "Mật khẩu cần có ít nhất một chữ hoa ('A'-'Z').";

                    ModelState.AddModelError(string.Empty, description);
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
                    $"Ensure that '{nameof(ApplicationUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
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
// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
notiConnection.on("ForceLogout", function () {
    Swal.fire({
        icon: 'error',
        title: 'Tài khoản đã bị khóa',
        text: 'Tài khoản của bạn đã bị khóa bởi quản trị viên.',
        allowOutsideClick: false,
        confirmButtonText: 'Đăng xuất ngay'
    }).then((result) => {
        window.location.href = '/Identity/Account/Login';
    });

    setTimeout(() => {
        window.location.href = '/Identity/Account/Login';
    }, 3000);
});
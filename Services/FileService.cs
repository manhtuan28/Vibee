namespace Vibee.Services
{
    public class FileService
    {
        private readonly IWebHostEnvironment _env;

        private readonly string[] _permittedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".mp4", ".mov" };

        private readonly Dictionary<string, List<byte[]>> _fileSignatures = new Dictionary<string, List<byte[]>>
        {
            { ".jpeg", new List<byte[]> { new byte[] { 0xFF, 0xD8, 0xFF } } },
            { ".jpg", new List<byte[]> { new byte[] { 0xFF, 0xD8, 0xFF } } },
            { ".png", new List<byte[]> { new byte[] { 0x89, 0x50, 0x4E, 0x47 } } },
            { ".mp4", new List<byte[]> { new byte[] { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70 }, new byte[] { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70 } } },
        };

        public FileService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<string> UploadFileAsync(IFormFile file, string folderName = "posts")
        {
            if (file == null || file.Length == 0) return null;

            if (file.Length > 50 * 1024 * 1024) throw new Exception("File quá lớn!");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !_permittedExtensions.Contains(ext))
            {
                throw new Exception("Định dạng file không được hỗ trợ.");
            }

            using (var reader = new BinaryReader(file.OpenReadStream()))
            {
                var signatures = _fileSignatures.ContainsKey(ext) ? _fileSignatures[ext] : null;
                var headerBytes = reader.ReadBytes(signatures?.Max(m => m.Length) ?? 0);
            }

            var newFileName = Guid.NewGuid().ToString() + ext;

            var uploadPath = Path.Combine(_env.WebRootPath, "uploads", folderName);

            if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

            var filePath = Path.Combine(uploadPath, newFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/{folderName}/{newFileName}";
        }
    }
}
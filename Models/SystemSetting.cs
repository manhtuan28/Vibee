using System.ComponentModel.DataAnnotations;

namespace Vibee.Models
{
    public class SystemSetting
    {
        [Key]
        public string Key { get; set; }
        public string Value { get; set; }
        public string Description { get; set; }
    }
}
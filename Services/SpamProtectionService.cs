using Microsoft.Extensions.Caching.Memory;
using Vibee.Data;
using Vibee.Models;

namespace Vibee.Services
{
    public class SpamProtectionService
    {
        private readonly IMemoryCache _cache;
        private readonly ApplicationDbContext _context;
        public SpamProtectionService(IMemoryCache cache, ApplicationDbContext context)
        {
            _cache = cache;
            _context = context;
        }

        public bool IsSystemActive()
        {
            return GetSettingInt("Spam_System_Active", 1) == 1;
        }

        private int GetSettingInt(string key, int defaultValue)
        {
            var setting = _context.SystemSettings.FirstOrDefault(s => s.Key == key);
            return setting != null && int.TryParse(setting.Value, out int val) ? val : defaultValue;
        }

        public bool IsBlocked(string ip, string type, out double remainingMinutes)
        {
            remainingMinutes = 0;

            if (!IsSystemActive()) return false;

            if (_cache.TryGetValue($"Block_{type}_{ip}", out DateTime unlockTime))
            {
                if (unlockTime > DateTime.Now)
                {
                    remainingMinutes = Math.Ceiling((unlockTime - DateTime.Now).TotalMinutes);
                    return true;
                }
            }

            var dbBlock = _context.IpBlocks
                            .FirstOrDefault(b => b.IpAddress == ip && b.Type == type && b.UnlockTime > DateTime.Now);

            if (dbBlock != null)
            {
                SetCacheBlock(ip, type, dbBlock.UnlockTime);
                remainingMinutes = Math.Ceiling((dbBlock.UnlockTime - DateTime.Now).TotalMinutes);
                return true;
            }

            return false;
        }

        public void TrackActivity(string ip, string type)
        {
            int limit = GetSettingInt($"Spam_{type}_Limit", type == "Register" ? 3 : 5);
            int window = GetSettingInt($"Spam_{type}_Window", type == "Register" ? 60 : 5);
            int blockTime = GetSettingInt($"Spam_{type}_BlockTime", type == "Register" ? 30 : 10);

            var countKey = $"Count_{type}_{ip}";
            _cache.TryGetValue(countKey, out int count);
            count++;

            if (count > limit)
            {
                var unlockTime = DateTime.Now.AddMinutes(blockTime);

                SetCacheBlock(ip, type, unlockTime);

                var oldBlock = _context.IpBlocks.FirstOrDefault(b => b.IpAddress == ip && b.Type == type);
                if (oldBlock != null) _context.IpBlocks.Remove(oldBlock);

                _context.IpBlocks.Add(new IpBlock
                {
                    IpAddress = ip,
                    Type = type,
                    BlockedAt = DateTime.Now,
                    UnlockTime = unlockTime,
                    Reason = $"Thực hiện {count} lần trong {window} phút"
                });
                _context.SaveChanges();

                _cache.Remove(countKey);
            }
            else
            {
                var options = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(window));
                _cache.Set(countKey, count, options);
            }
        }

        private void SetCacheBlock(string ip, string type, DateTime unlockTime)
        {
            var options = new MemoryCacheEntryOptions().SetAbsoluteExpiration(unlockTime);
            _cache.Set($"Block_{type}_{ip}", unlockTime, options);
        }

        public void UnlockIp(int blockId)
        {
            var block = _context.IpBlocks.Find(blockId);
            if (block != null)
            {
                _cache.Remove($"Block_{block.Type}_{block.IpAddress}");
                _context.IpBlocks.Remove(block);
                _context.SaveChanges();
            }
        }
    }
}
namespace IdentityService.Entities
{
    /// <summary>
    /// Refresh Token entity - kullanıcıların oturum sürekliliği için kullanılır
    /// </summary>
    public class RefreshToken
    {
        public int Id { get; set; }
        
        /// <summary>
        /// Token değeri (güvenli rastgele string)
        /// </summary>
        public string Token { get; set; } = string.Empty;
        
        /// <summary>
        /// Hangi kullanıcıya ait (Foreign Key)
        /// </summary>
        public string UserId { get; set; } = string.Empty;
        
        /// <summary>
        /// Token oluşturulma zamanı
        /// </summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// Token son kullanma tarihi
        /// </summary>
        public DateTime ExpiresAt { get; set; }
        
        /// <summary>
        /// Token iptal edilmiş mi?
        /// </summary>
        public bool IsRevoked { get; set; }
        
        /// <summary>
        /// Token kullanıldı mı? (tek kullanımlık)
        /// </summary>
        public bool IsUsed { get; set; }
        
        /// <summary>
        /// Token'ı hangi IP adresi oluşturdu
        /// </summary>
        public string? CreatedByIp { get; set; }
        
        /// <summary>
        /// Token'ı hangi IP adresi kullandı
        /// </summary>
        public string? UsedByIp { get; set; }
        
        /// <summary>
        /// Token aktif mi? (süresi geçmemiş, iptal edilmemiş, kullanılmamış)
        /// </summary>
        public bool IsActive => !IsRevoked && !IsUsed && DateTime.UtcNow < ExpiresAt;
        
        // Navigation property
        public AppUser User { get; set; } = null!;
    }
}

using System.Data.Entity.ModelConfiguration;
using BeastPc.Models.Tables;

namespace BeastPc.Models.Maps
{
    public class UserMap : EntityTypeConfiguration<User>
    {
        public UserMap()
        {
            ToTable("users");
            HasKey(x => x.Id);

            Property(x => x.Id).HasColumnName("id");
            Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(80).IsRequired();
            Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(80).IsRequired();
            Property(x => x.Username).HasColumnName("username").HasMaxLength(88).IsRequired();
            Property(x => x.Email).HasColumnName("email").HasMaxLength(100).IsRequired();
            Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
            Property(x => x.Role).HasColumnName("role").HasMaxLength(10).IsRequired();
            Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        }
    }
}

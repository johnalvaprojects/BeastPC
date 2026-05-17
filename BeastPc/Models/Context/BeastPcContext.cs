using BeastPc.Models.Maps;
using BeastPc.Models.Tables;
using System.Data.Entity;

namespace BeastPc.Models.Context
{
    public class BeastPcContext : DbContext
    {
        static BeastPcContext()
        {
            Database.SetInitializer<BeastPcContext>(null);
        }

        public BeastPcContext()
            : base("name=BeastPcContext")
        {
        }

        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<PcBuild> PcBuilds { get; set; }
        public virtual DbSet<Order> Orders { get; set; }
        public virtual DbSet<OrderItem> OrderItems { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Configurations.Add(new UserMap());
            modelBuilder.Configurations.Add(new PcBuildMap());
            modelBuilder.Configurations.Add(new OrderMap());
            modelBuilder.Configurations.Add(new OrderItemMap());
        }
    }
}

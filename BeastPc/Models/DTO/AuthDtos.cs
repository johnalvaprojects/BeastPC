namespace BeastPc.Models.DTO
{
    public class RegisterDto
    {
        public int? Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class LoginDto
    {
        public string UsernameOrEmail { get; set; }
        public string Password { get; set; }
    }

    public class DeleteDto
    {
        public int Id { get; set; }
    }

    public class OrderStatusDto
    {
        public long Id { get; set; }
        public string Status { get; set; }
    }

    public class RoleChangeDto
    {
        public int Id { get; set; }
        public string Role { get; set; }
    }

    public class PlaceOrderItemDto
    {
        public int BuildId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class PlaceOrderDto
    {
        public int UserId { get; set; }
        public PlaceOrderItemDto[] Items { get; set; }
    }

    /// <summary>Admin dashboard KPI card definition (stored in MySQL).</summary>
    public class DashboardCardDto
    {
        public int Id { get; set; }
        public int SortOrder { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Accent { get; set; }
        /// <summary>builds | orders | users | revenue | pending | literal</summary>
        public string MetricKey { get; set; }
        public string LiteralValue { get; set; }
        public bool IsActive { get; set; }
    }
}

using System;
using System.Security.Cryptography;
using System.Text;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using BeastPc.Models.DTO;
using BeastPc.Models.Tables;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BeastPc.Controllers
{
    public class BeastPcController : Controller
    {
        private static string GetConnString()
        {
            return ConfigurationManager.ConnectionStrings["BeastPcContext"]?.ConnectionString;
        }

        private static MySqlConnection OpenConn()
        {
            var cs = GetConnString();
            if (string.IsNullOrWhiteSpace(cs)) throw new Exception("Missing BeastPcContext connection string.");
            var con = new MySqlConnection(cs);
            con.Open();
            return con;
        }

        private static bool HasColumn(MySqlConnection con, string table, string column)
        {
            using (var cmd = new MySqlCommand(@"
                SELECT COUNT(*)
                FROM information_schema.columns
                WHERE table_schema = DATABASE()
                  AND table_name = @t
                  AND column_name = @c;", con))
            {
                cmd.Parameters.AddWithValue("@t", table);
                cmd.Parameters.AddWithValue("@c", column);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        private static string ResolveColumn(MySqlConnection con, string table, params string[] candidates)
        {
            foreach (var c in candidates)
            {
                if (!string.IsNullOrWhiteSpace(c) && HasColumn(con, table, c)) return c;
            }
            return null;
        }

        /// <summary>Maps app BuildId to actual MySQL column on order_items (schemas vary).</summary>
        private static string ResolveOrderItemsBuildColumn(MySqlConnection con)
        {
            return ResolveColumn(con, "order_items", "build_id", "pc_build_id", "product_id", "fk_build_id");
        }

        private static string ResolveOrderItemsOrderFkColumn(MySqlConnection con)
        {
            return ResolveColumn(con, "order_items", "order_id", "fk_order_id", "OrderId");
        }

        private static string ResolveOrderItemsQtyColumn(MySqlConnection con)
        {
            return ResolveColumn(con, "order_items", "quantity", "qty", "amount", "item_quantity", "order_qty");
        }

        private static string ResolveOrderItemsUnitPriceColumn(MySqlConnection con)
        {
            return ResolveColumn(con, "order_items", "unit_price", "price", "unitprice");
        }

        private static void EnsureOrderStatusSchema(MySqlConnection con)
        {
            string columnType = null;
            using (var cmd = new MySqlCommand(@"
                SELECT COLUMN_TYPE
                FROM information_schema.columns
                WHERE table_schema = DATABASE()
                  AND table_name = 'orders'
                  AND column_name = 'status'
                LIMIT 1;", con))
            {
                columnType = Convert.ToString(cmd.ExecuteScalar());
            }

            if (string.IsNullOrWhiteSpace(columnType)) return;

            var t = columnType.ToLowerInvariant();
            if (t.Contains("'processing'") && t.Contains("'shipped'") && t.Contains("'delivered'"))
                return;

            // Old schema was ENUM('pending','paid','cancelled'); expand first so data can be normalized safely.
            using (var cmd = new MySqlCommand(@"
                ALTER TABLE orders
                MODIFY status ENUM('pending','paid','processing','shipped','delivered','cancelled')
                NOT NULL DEFAULT 'pending';", con))
            {
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new MySqlCommand(@"
                UPDATE orders
                SET status = CASE
                    WHEN status = 'paid' THEN 'delivered'
                    WHEN status = '' THEN 'pending'
                    ELSE status
                END;", con))
            {
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new MySqlCommand(@"
                ALTER TABLE orders
                MODIFY status ENUM('pending','processing','shipped','delivered','cancelled')
                NOT NULL DEFAULT 'pending';", con))
            {
                cmd.ExecuteNonQuery();
            }
        }

        [HttpGet]
        public JsonResult DescribePcBuilds()
        {
            try
            {
                using (var con = OpenConn())
                using (var cmd = new MySqlCommand(@"
                    SELECT column_name, data_type, is_nullable
                    FROM information_schema.columns
                    WHERE table_schema = DATABASE()
                      AND table_name = 'pc_builds'
                    ORDER BY ordinal_position;", con))
                using (var r = cmd.ExecuteReader())
                {
                    var cols = new System.Collections.Generic.List<object>();
                    while (r.Read())
                    {
                        cols.Add(new
                        {
                            name = r.GetString("column_name"),
                            type = r.GetString("data_type"),
                            nullable = r.GetString("is_nullable")
                        });
                    }
                    return Json(new { ok = true, columns = cols }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult PingDb()
        {
            try
            {
                using (var con = OpenConn())
                    return Json(new { ok = true, ts = DateTime.UtcNow, serverVersion = con.ServerVersion }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    ok = false,
                    error = ex.Message,
                    inner = ex.InnerException != null ? ex.InnerException.Message : null,
                    inner2 = ex.InnerException != null && ex.InnerException.InnerException != null ? ex.InnerException.InnerException.Message : null
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult PingDbRaw()
        {
            try
            {
                using (var con = OpenConn())
                {
                    return Json(new
                    {
                        ok = true,
                        serverVersion = con.ServerVersion,
                        state = con.State.ToString()
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    ok = false,
                    error = ex.Message,
                    inner = ex.InnerException != null ? ex.InnerException.Message : null,
                    inner2 = ex.InnerException != null && ex.InnerException.InnerException != null ? ex.InnerException.InnerException.Message : null
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult Register(RegisterDto dto)
        {
            try
            {
                if (dto == null) return Json(new { ok = false, error = "Missing payload." });
                if (string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName))
                    return Json(new { ok = false, error = "First name and last name are required." });
                if (string.IsNullOrWhiteSpace(dto.Username))
                    return Json(new { ok = false, error = "Username is required." });
                if (string.IsNullOrWhiteSpace(dto.Email))
                    return Json(new { ok = false, error = "Email is required." });
                if (string.IsNullOrWhiteSpace(dto.Password))
                    return Json(new { ok = false, error = "Password is required." });

                var fn = (dto.FirstName ?? "").Trim();
                var ln = (dto.LastName ?? "").Trim();
                var username = (dto.Username ?? "").Trim();
                var email = (dto.Email ?? "").Trim();

                if (fn.Length == 0 || ln.Length == 0)
                    return Json(new { ok = false, error = "First name and last name are required." });
                if (fn.Length > 80 || ln.Length > 80)
                    return Json(new { ok = false, error = "First/last name must be 80 characters or fewer." });
                if (username.Length < 3 || username.Length > 32)
                    return Json(new { ok = false, error = "Username must be 3–32 characters." });
                if (!Regex.IsMatch(username, @"^[A-Za-z0-9_]+$"))
                    return Json(new { ok = false, error = "Username may only contain letters, numbers, and underscore." });
                if (email.Length == 0 || email.Length > 254)
                    return Json(new { ok = false, error = "Email is required (max 254 characters)." });
                if (!Regex.IsMatch(email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
                    return Json(new { ok = false, error = "Enter a valid email address." });
                if (dto.Password.Length < 8 || dto.Password.Length > 128)
                    return Json(new { ok = false, error = "Password must be 8–128 characters." });

                var passHash = Sha256Hex(dto.Password);

                using (var con = OpenConn())
                {
                    // Uniqueness checks
                    using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM users WHERE email=@e", con))
                    {
                        cmd.Parameters.AddWithValue("@e", email);
                        var c = Convert.ToInt32(cmd.ExecuteScalar());
                        if (c > 0) return Json(new { ok = false, error = "Email already exists." });
                    }
                    using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM users WHERE username=@u", con))
                    {
                        cmd.Parameters.AddWithValue("@u", username);
                        var c = Convert.ToInt32(cmd.ExecuteScalar());
                        if (c > 0) return Json(new { ok = false, error = "Username already exists." });
                    }

                    var sql = @"INSERT INTO users (first_name,last_name,username,email,password_hash,role,created_at)
                                VALUES (@fn,@ln,@un,@em,@ph,'user',UTC_TIMESTAMP());
                                SELECT LAST_INSERT_ID();";

                    long id;
                    using (var cmd = new MySqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@fn", fn);
                        cmd.Parameters.AddWithValue("@ln", ln);
                        cmd.Parameters.AddWithValue("@un", username);
                        cmd.Parameters.AddWithValue("@em", email);
                        cmd.Parameters.AddWithValue("@ph", passHash);
                        id = Convert.ToInt64(cmd.ExecuteScalar());
                    }

                    return Json(new
                    {
                        ok = true,
                        user = new { Id = id, FirstName = fn, LastName = ln, Username = username, Email = email, Role = "user" }
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    ok = false,
                    error = ex.Message,
                    inner = ex.InnerException != null ? ex.InnerException.Message : null,
                    inner2 = ex.InnerException != null && ex.InnerException.InnerException != null ? ex.InnerException.InnerException.Message : null
                });
            }
        }

        [HttpPost]
        public JsonResult Login(LoginDto dto)
        {
            try
            {
                if (dto == null) return Json(new { ok = false, error = "Missing payload." });
                if (string.IsNullOrWhiteSpace(dto.UsernameOrEmail) || string.IsNullOrWhiteSpace(dto.Password))
                    return Json(new { ok = false, error = "Enter username/email and password." });

                var key = dto.UsernameOrEmail.Trim();
                var passHash = Sha256Hex(dto.Password);

                using (var con = OpenConn())
                {
                    var sql = @"SELECT id, first_name, last_name, username, email, password_hash, role
                                FROM users
                                WHERE username=@k OR email=@k
                                LIMIT 1;";

                    using (var cmd = new MySqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@k", key);
                        using (var r = cmd.ExecuteReader())
                        {
                            if (!r.Read()) return Json(new { ok = false, error = "User not found." });
                            var stored = r.GetString("password_hash");
                            if (!string.Equals(stored, passHash, StringComparison.OrdinalIgnoreCase))
                                return Json(new { ok = false, error = "Invalid password." });

                            var user = new
                            {
                                Id = r.GetInt64("id"),
                                FirstName = r.GetString("first_name"),
                                LastName = r.GetString("last_name"),
                                Username = r.GetString("username"),
                                Email = r.GetString("email"),
                                Role = r.GetString("role")
                            };
                            return Json(new { ok = true, user });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    ok = false,
                    error = ex.Message,
                    inner = ex.InnerException != null ? ex.InnerException.Message : null,
                    inner2 = ex.InnerException != null && ex.InnerException.InnerException != null ? ex.InnerException.InnerException.Message : null
                });
            }
        }

        [HttpGet]
        public JsonResult SearchUsers(string q)
        {
            try
            {
                q = (q ?? "").Trim();
                using (var con = OpenConn())
                {
                    var like = "%" + q + "%";
                    var sql = @"SELECT id, first_name, last_name, username, email, role, created_at
                                FROM users
                                WHERE @q = '' OR first_name LIKE @like OR last_name LIKE @like OR username LIKE @like OR email LIKE @like
                                ORDER BY id DESC
                                LIMIT 50;";

                    var users = new System.Collections.Generic.List<object>();
                    using (var cmd = new MySqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@q", q);
                        cmd.Parameters.AddWithValue("@like", like);
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                users.Add(new
                                {
                                    Id = r.GetInt64("id"),
                                    FirstName = r.GetString("first_name"),
                                    LastName = r.GetString("last_name"),
                                    Username = r.GetString("username"),
                                    Email = r.GetString("email"),
                                    Role = r.GetString("role"),
                                    CreatedAt = r.GetDateTime("created_at")
                                });
                            }
                        }
                    }

                    return Json(new { ok = true, users }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // ─── Builds ───────────────────────────────────────────────────────────

        [HttpPost]
        public JsonResult UploadBuildImage()
        {
            try
            {
                var file = Request?.Files?["file"];
                if (file == null || file.ContentLength <= 0)
                    return Json(new { ok = false, error = "No file uploaded." });

                // Basic validation (keep simple for class demo)
                var maxBytes = 5 * 1024 * 1024; // 5MB
                if (file.ContentLength > maxBytes)
                    return Json(new { ok = false, error = "File too large (max 5MB)." });

                var ext = (Path.GetExtension(file.FileName) ?? "").ToLowerInvariant();
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
                if (!allowed.Contains(ext))
                    return Json(new { ok = false, error = "Unsupported file type." });

                // Save under /Content/uploads/builds/
                var relDir = "~/Content/uploads/builds";
                var absDir = Server.MapPath(relDir);
                if (!Directory.Exists(absDir)) Directory.CreateDirectory(absDir);

                var safeBase = Path.GetFileNameWithoutExtension(file.FileName);
                safeBase = string.IsNullOrWhiteSpace(safeBase) ? "build" : safeBase;
                safeBase = string.Concat(safeBase.Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_'));
                if (string.IsNullOrWhiteSpace(safeBase)) safeBase = "build";

                var fname = safeBase + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ext;
                var absPath = Path.Combine(absDir, fname);
                file.SaveAs(absPath);

                var url = Url.Content(relDir + "/" + fname);
                return Json(new { ok = true, url });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetBuilds()
        {
            try
            {
                using (var con = OpenConn())
                {
                    var hasActive = HasColumn(con, "pc_builds", "active");
                    var hasIsActive = HasColumn(con, "pc_builds", "is_active");
                    var activeCol = hasActive ? "active" : (hasIsActive ? "is_active" : null);
                    var nameCol = ResolveColumn(con, "pc_builds", "name", "build_name");
                    var descCol = ResolveColumn(con, "pc_builds", "description", "tagline");
                    var imgCol = ResolveColumn(con, "pc_builds", "image_url", "image", "img", "imagePath");
                    var coolingCol = ResolveColumn(con, "pc_builds", "cooling");
                    var psuCol = ResolveColumn(con, "pc_builds", "psu");
                    var caseCol = ResolveColumn(con, "pc_builds", "case_name", "case");
                    var createdCol = ResolveColumn(con, "pc_builds", "created_at", "createdAt");
                    if (string.IsNullOrWhiteSpace(nameCol)) throw new Exception("pc_builds missing name/build_name column.");

                    var descExpr = string.IsNullOrWhiteSpace(descCol) ? "NULL" : descCol;
                    var imgExpr = string.IsNullOrWhiteSpace(imgCol) ? "NULL" : imgCol;
                    var coolingExpr = string.IsNullOrWhiteSpace(coolingCol) ? "NULL" : coolingCol;
                    var psuExpr = string.IsNullOrWhiteSpace(psuCol) ? "NULL" : psuCol;
                    var caseExpr = string.IsNullOrWhiteSpace(caseCol) ? "NULL" : caseCol;
                    var createdExpr = string.IsNullOrWhiteSpace(createdCol) ? "UTC_TIMESTAMP()" : createdCol;
                    var activeExpr = string.IsNullOrWhiteSpace(activeCol) ? "1" : activeCol;

                    var sql =
                        $@"SELECT id,
                                  {nameCol} AS name,
                                  {descExpr} AS description,
                                  price, cpu, gpu, ram, storage,
                                  {coolingExpr} AS cooling,
                                  {psuExpr} AS psu,
                                  {caseExpr} AS case_name,
                                  {imgExpr} AS image_url,
                                  stock,
                                  {activeExpr} AS active,
                                  {createdExpr} AS created_at
                           FROM pc_builds
                           ORDER BY id ASC;";

                    var builds = new System.Collections.Generic.List<object>();
                    using (var cmd = new MySqlCommand(sql, con))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            builds.Add(new
                            {
                                Id = r.GetInt64("id"),
                                Name = r.IsDBNull(r.GetOrdinal("name")) ? null : r.GetString("name"),
                                Description = r.IsDBNull(r.GetOrdinal("description")) ? null : r.GetString("description"),
                                Price = r.GetDecimal("price"),
                                Cpu = r.IsDBNull(r.GetOrdinal("cpu")) ? null : r.GetString("cpu"),
                                Gpu = r.IsDBNull(r.GetOrdinal("gpu")) ? null : r.GetString("gpu"),
                                Ram = r.IsDBNull(r.GetOrdinal("ram")) ? null : r.GetString("ram"),
                                Storage = r.IsDBNull(r.GetOrdinal("storage")) ? null : r.GetString("storage"),
                                Cooling = r.IsDBNull(r.GetOrdinal("cooling")) ? null : r.GetString("cooling"),
                                Psu = r.IsDBNull(r.GetOrdinal("psu")) ? null : r.GetString("psu"),
                                CaseName = r.IsDBNull(r.GetOrdinal("case_name")) ? null : r.GetString("case_name"),
                                ImageUrl = r.IsDBNull(r.GetOrdinal("image_url")) ? null : r.GetString("image_url"),
                                Stock = r.GetInt32("stock"),
                                Active = (!string.IsNullOrWhiteSpace(activeCol)) ? r.GetBoolean("active") : true,
                                CreatedAt = r.GetDateTime("created_at")
                            });
                        }
                    }
                    return Json(new { ok = true, builds }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult UpsertBuild()
        {
            var dto = ReadRequestJson<PcBuild>();
            try
            {
                if (dto == null || string.IsNullOrWhiteSpace(dto.Name))
                    return Json(new { ok = false, error = "Build name is required." });

                using (var con = OpenConn())
                {
                    var hasActive = HasColumn(con, "pc_builds", "active");
                    var hasIsActive = HasColumn(con, "pc_builds", "is_active");
                    var activeCol = hasActive ? "active" : (hasIsActive ? "is_active" : null);
                    var hasActiveCol = !string.IsNullOrWhiteSpace(activeCol);
                    var nameCol = ResolveColumn(con, "pc_builds", "name", "build_name");
                    var descCol = ResolveColumn(con, "pc_builds", "description", "tagline");
                    var imgCol = ResolveColumn(con, "pc_builds", "image_url", "image", "img", "imagePath");
                    var coolingCol = ResolveColumn(con, "pc_builds", "cooling");
                    var psuCol = ResolveColumn(con, "pc_builds", "psu");
                    var caseCol = ResolveColumn(con, "pc_builds", "case_name", "case");
                    var hasCooling = !string.IsNullOrWhiteSpace(coolingCol);
                    var hasPsu = !string.IsNullOrWhiteSpace(psuCol);
                    var hasCase = !string.IsNullOrWhiteSpace(caseCol);
                    if (string.IsNullOrWhiteSpace(nameCol)) throw new Exception("pc_builds missing name/build_name column.");
                    var descSet = descCol ?? "description";
                    var imgSet = imgCol ?? "image_url";

                    if (dto.Id > 0)
                    {
                        var extraSet = (hasCooling ? $", {coolingCol}=@cool" : "")
                                     + (hasPsu ? $", {psuCol}=@psu" : "")
                                     + (hasCase ? $", {caseCol}=@case" : "");
                        var sql = hasActiveCol
                            ? $@"UPDATE pc_builds
                                SET {nameCol}=@n, {descSet}=@d, price=@p, cpu=@cpu, gpu=@gpu, ram=@ram, storage=@st{extraSet}, {imgSet}=@img, stock=@stock, {activeCol}=@a
                                WHERE id=@id;"
                            : $@"UPDATE pc_builds
                                SET {nameCol}=@n, {descSet}=@d, price=@p, cpu=@cpu, gpu=@gpu, ram=@ram, storage=@st{extraSet}, {imgSet}=@img, stock=@stock
                                WHERE id=@id;";

                        using (var cmd = new MySqlCommand(sql, con))
                        {
                            cmd.Parameters.AddWithValue("@id", dto.Id);
                            cmd.Parameters.AddWithValue("@n", dto.Name);
                            cmd.Parameters.AddWithValue("@d", (object)dto.Description ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@p", dto.Price);
                            cmd.Parameters.AddWithValue("@cpu", (object)dto.Cpu ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@gpu", (object)dto.Gpu ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ram", (object)dto.Ram ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@st", (object)dto.Storage ?? DBNull.Value);
                            if (hasCooling) cmd.Parameters.AddWithValue("@cool", (object)dto.Cooling ?? DBNull.Value);
                            if (hasPsu) cmd.Parameters.AddWithValue("@psu", (object)dto.Psu ?? DBNull.Value);
                            if (hasCase) cmd.Parameters.AddWithValue("@case", (object)dto.CaseName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@img", (object)dto.ImageUrl ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@stock", dto.Stock);
                            if (hasActiveCol) cmd.Parameters.AddWithValue("@a", dto.Active ? 1 : 0);
                            var rows = cmd.ExecuteNonQuery();
                            if (rows <= 0) return Json(new { ok = false, error = "Build not found." });
                        }
                    }
                    else
                    {
                        var descInsert = descCol ?? "description";
                        var imgInsert = imgCol ?? "image_url";
                        var extraCols = (hasCooling ? $", {coolingCol}" : "")
                                      + (hasPsu ? $", {psuCol}" : "")
                                      + (hasCase ? $", {caseCol}" : "");
                        var extraVals = (hasCooling ? ", @cool" : "")
                                      + (hasPsu ? ", @psu" : "")
                                      + (hasCase ? ", @case" : "");

                        var sql = hasActiveCol
                            ? $@"INSERT INTO pc_builds ({nameCol}, {descInsert}, price, cpu, gpu, ram, storage{extraCols}, {imgInsert}, stock, {activeCol}, created_at)
                                VALUES (@n,@d,@p,@cpu,@gpu,@ram,@st{extraVals},@img,@stock,@a,UTC_TIMESTAMP());"
                            : $@"INSERT INTO pc_builds ({nameCol}, {descInsert}, price, cpu, gpu, ram, storage{extraCols}, {imgInsert}, stock, created_at)
                                VALUES (@n,@d,@p,@cpu,@gpu,@ram,@st{extraVals},@img,@stock,UTC_TIMESTAMP());";

                        using (var cmd = new MySqlCommand(sql, con))
                        {
                            cmd.Parameters.AddWithValue("@n", dto.Name);
                            cmd.Parameters.AddWithValue("@d", (object)dto.Description ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@p", dto.Price);
                            cmd.Parameters.AddWithValue("@cpu", (object)dto.Cpu ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@gpu", (object)dto.Gpu ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ram", (object)dto.Ram ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@st", (object)dto.Storage ?? DBNull.Value);
                            if (hasCooling) cmd.Parameters.AddWithValue("@cool", (object)dto.Cooling ?? DBNull.Value);
                            if (hasPsu) cmd.Parameters.AddWithValue("@psu", (object)dto.Psu ?? DBNull.Value);
                            if (hasCase) cmd.Parameters.AddWithValue("@case", (object)dto.CaseName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@img", (object)dto.ImageUrl ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@stock", dto.Stock);
                            if (hasActiveCol) cmd.Parameters.AddWithValue("@a", dto.Active ? 1 : 0);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    return Json(new { ok = true });
                }
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult DeleteBuild()
        {
            var dto = ReadRequestJson<DeleteDto>();
            try
            {
                if (dto == null || dto.Id <= 0) return Json(new { ok = false, error = "Invalid build id." });
                using (var con = OpenConn())
                {
                    using (var cmd = new MySqlCommand("DELETE FROM pc_builds WHERE id=@id", con))
                    {
                        cmd.Parameters.AddWithValue("@id", dto.Id);
                        var rows = cmd.ExecuteNonQuery();
                        if (rows <= 0) return Json(new { ok = false, error = "Not found." });
                    }
                    return Json(new { ok = true });
                }
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message });
            }
        }

        // ─── Dashboard (cards + charts) ───────────────────────────────────────

        [HttpGet]
        public JsonResult GetDashboardCards()
        {
            try
            {
                using (var con = OpenConn())
                {
                    EnsureAdminDashboardCardsTable(con);
                    SeedDefaultDashboardCardsIfEmpty(con);

                    var snap = LoadDashboardSnapshot(con);
                    var cards = new System.Collections.Generic.List<object>();

                    using (var cmd = new MySqlCommand(@"
                        SELECT title, subtitle, accent, metric_key, literal_value
                        FROM admin_dashboard_cards
                        WHERE is_active = 1
                        ORDER BY sort_order ASC, id ASC;", con))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var title = r.GetString("title");
                            var subtitle = r.IsDBNull(r.GetOrdinal("subtitle")) ? null : r.GetString("subtitle");
                            var accent = r.IsDBNull(r.GetOrdinal("accent")) ? null : r.GetString("accent");
                            var metricKey = r.GetString("metric_key");
                            var literal = r.IsDBNull(r.GetOrdinal("literal_value")) ? null : r.GetString("literal_value");
                            var value = FormatDashboardCardValue(snap, metricKey, literal);
                            cards.Add(new { Title = title, Value = value, Subtitle = subtitle, Accent = accent });
                        }
                    }

                    return Json(new { ok = true, cards }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetDashboardCardDefinitions()
        {
            try
            {
                using (var con = OpenConn())
                {
                    EnsureAdminDashboardCardsTable(con);
                    SeedDefaultDashboardCardsIfEmpty(con);

                    var rows = new System.Collections.Generic.List<object>();
                    using (var cmd = new MySqlCommand(@"
                        SELECT id, sort_order, title, subtitle, accent, metric_key, literal_value, is_active
                        FROM admin_dashboard_cards
                        ORDER BY sort_order ASC, id ASC;", con))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            rows.Add(new
                            {
                                Id = r.GetInt32("id"),
                                SortOrder = r.GetInt32("sort_order"),
                                Title = r.GetString("title"),
                                Subtitle = r.IsDBNull(r.GetOrdinal("subtitle")) ? null : r.GetString("subtitle"),
                                Accent = r.IsDBNull(r.GetOrdinal("accent")) ? null : r.GetString("accent"),
                                MetricKey = r.GetString("metric_key"),
                                LiteralValue = r.IsDBNull(r.GetOrdinal("literal_value")) ? null : r.GetString("literal_value"),
                                IsActive = r.GetBoolean("is_active")
                            });
                        }
                    }

                    return Json(new { ok = true, cards = rows }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult SaveDashboardCard()
        {
            var dto = ReadRequestJson<DashboardCardDto>();
            try
            {
                if (dto == null) return Json(new { ok = false, error = "Missing payload." });
                if (string.IsNullOrWhiteSpace(dto.Title)) return Json(new { ok = false, error = "Title is required." });
                if (!IsValidMetricKey(dto.MetricKey)) return Json(new { ok = false, error = "Invalid metric key." });
                if (string.Equals((dto.MetricKey ?? "").Trim(), "literal", StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrWhiteSpace(dto.LiteralValue))
                    return Json(new { ok = false, error = "Literal cards need a value." });

                using (var con = OpenConn())
                {
                    EnsureAdminDashboardCardsTable(con);

                    if (dto.Id > 0)
                    {
                        using (var cmd = new MySqlCommand(@"
UPDATE admin_dashboard_cards
SET sort_order=@s, title=@t, subtitle=@st, accent=@a, metric_key=@m, literal_value=@lv, is_active=@ia
WHERE id=@id;", con))
                        {
                            cmd.Parameters.AddWithValue("@id", dto.Id);
                            cmd.Parameters.AddWithValue("@s", dto.SortOrder);
                            cmd.Parameters.AddWithValue("@t", dto.Title.Trim());
                            cmd.Parameters.AddWithValue("@st", (object)dto.Subtitle ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@a", (object)dto.Accent ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@m", dto.MetricKey.Trim().ToLowerInvariant());
                            cmd.Parameters.AddWithValue("@lv", (object)dto.LiteralValue ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ia", dto.IsActive ? 1 : 0);
                            var rows = cmd.ExecuteNonQuery();
                            if (rows <= 0) return Json(new { ok = false, error = "Card not found." });
                        }
                    }
                    else
                    {
                        using (var cmd = new MySqlCommand(@"
INSERT INTO admin_dashboard_cards (sort_order, title, subtitle, accent, metric_key, literal_value, is_active)
VALUES (@s,@t,@st,@a,@m,@lv,@ia);", con))
                        {
                            cmd.Parameters.AddWithValue("@s", dto.SortOrder);
                            cmd.Parameters.AddWithValue("@t", dto.Title.Trim());
                            cmd.Parameters.AddWithValue("@st", (object)dto.Subtitle ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@a", (object)dto.Accent ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@m", dto.MetricKey.Trim().ToLowerInvariant());
                            cmd.Parameters.AddWithValue("@lv", (object)dto.LiteralValue ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ia", dto.IsActive ? 1 : 0);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    return Json(new { ok = true });
                }
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult DeleteDashboardCard()
        {
            var dto = ReadRequestJson<DeleteDto>();
            try
            {
                if (dto == null || dto.Id <= 0) return Json(new { ok = false, error = "Invalid id." });
                using (var con = OpenConn())
                {
                    EnsureAdminDashboardCardsTable(con);
                    using (var cmd = new MySqlCommand("DELETE FROM admin_dashboard_cards WHERE id=@id;", con))
                    {
                        cmd.Parameters.AddWithValue("@id", dto.Id);
                        var rows = cmd.ExecuteNonQuery();
                        if (rows <= 0) return Json(new { ok = false, error = "Card not found." });
                    }
                    return Json(new { ok = true });
                }
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetOrdersStatusChart()
        {
            try
            {
                using (var con = OpenConn())
                {
                    EnsureOrderStatusSchema(con);
                    var labels = new System.Collections.Generic.List<string>();
                    var data = new System.Collections.Generic.List<int>();
                    using (var cmd = new MySqlCommand(@"
                        SELECT status, COUNT(*) AS c
                        FROM orders
                        GROUP BY status
                        ORDER BY c DESC;", con))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            labels.Add(r.GetString("status"));
                            data.Add(Convert.ToInt32(r.GetInt64("c")));
                        }
                    }
                    return Json(new { ok = true, labels, data }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // ─── Orders ───────────────────────────────────────────────────────────

        [HttpGet]
        public JsonResult GetOrders()
        {
            try
            {
                using (var con = OpenConn())
                {
                    EnsureOrderStatusSchema(con);
                    var sql = @"SELECT id, user_id, total_amount, status, created_at
                                FROM orders
                                ORDER BY id DESC;";
                    var orders = new System.Collections.Generic.List<object>();
                    using (var cmd = new MySqlCommand(sql, con))
                    using (var r = cmd.ExecuteReader())
                    {
                        var ordStatus = r.GetOrdinal("status");
                        while (r.Read())
                        {
                            var st = r.IsDBNull(ordStatus) ? "" : (r.GetString(ordStatus) ?? "").Trim();
                            if (string.IsNullOrEmpty(st)) st = "pending";
                            orders.Add(new
                            {
                                Id = r.GetInt64("id"),
                                UserId = r.GetInt64("user_id"),
                                TotalAmount = r.GetDecimal("total_amount"),
                                Status = st,
                                CreatedAt = r.GetDateTime("created_at")
                            });
                        }
                    }
                    return Json(new { ok = true, orders }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetOrdersByUser(long userId)
        {
            try
            {
                if (userId <= 0) return Json(new { ok = false, error = "Invalid user id." }, JsonRequestBehavior.AllowGet);
                using (var con = OpenConn())
                {
                    EnsureOrderStatusSchema(con);
                    var sql = @"SELECT id, user_id, total_amount, status, created_at
                                FROM orders
                                WHERE user_id=@uid
                                ORDER BY id DESC;";
                    var orders = new System.Collections.Generic.List<object>();
                    using (var cmd = new MySqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@uid", userId);
                        using (var r = cmd.ExecuteReader())
                        {
                            var ordStatus = r.GetOrdinal("status");
                            while (r.Read())
                            {
                                var st = r.IsDBNull(ordStatus) ? "" : (r.GetString(ordStatus) ?? "").Trim();
                                if (string.IsNullOrEmpty(st)) st = "pending";
                                orders.Add(new
                                {
                                    Id = r.GetInt64("id"),
                                    UserId = r.GetInt64("user_id"),
                                    TotalAmount = r.GetDecimal("total_amount"),
                                    Status = st,
                                    CreatedAt = r.GetDateTime("created_at")
                                });
                            }
                        }
                    }
                    return Json(new { ok = true, orders }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult UpdateOrderStatus()
        {
            try
            {
                var body = ReadEntireRequestBody();
                if (string.IsNullOrWhiteSpace(body))
                    return Json(new { ok = false, error = "Missing payload." });

                long id = 0;
                string statusRaw = null;
                try
                {
                    var jo = JObject.Parse(body);
                    var idTok = jo["id"] ?? jo["Id"] ?? jo["ID"];
                    if (idTok != null && idTok.Type != JTokenType.Null)
                    {
                        if (idTok.Type == JTokenType.Integer || idTok.Type == JTokenType.Float)
                            id = idTok.Value<long>();
                        else
                            long.TryParse(idTok.ToString().Trim(), out id);
                    }
                    var stTok = jo["status"] ?? jo["Status"];
                    if (stTok != null && stTok.Type != JTokenType.Null)
                        statusRaw = stTok.ToString();
                }
                catch
                {
                    return Json(new { ok = false, error = "Invalid JSON." });
                }

                if (id <= 0) return Json(new { ok = false, error = "Invalid order." });

                var allowed = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
                {
                    "pending", "processing", "shipped", "delivered", "cancelled"
                };
                var s = string.IsNullOrWhiteSpace(statusRaw) ? "" : statusRaw.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(s) || !allowed.Contains(s))
                    return Json(new { ok = false, error = "Invalid or missing status." });

                using (var con = OpenConn())
                {
                    EnsureOrderStatusSchema(con);
                    using (var cmd = new MySqlCommand("UPDATE orders SET status=@s WHERE id=@id", con))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.Parameters.AddWithValue("@s", s);
                        var rows = cmd.ExecuteNonQuery();
                        if (rows <= 0) return Json(new { ok = false, error = "Order not found." });
                    }
                    return Json(new { ok = true });
                }
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message });
            }
        }

        /// <summary>Customer cancels own order while still pending or processing (same trust model as PlaceOrder: UserId from client).</summary>
        [HttpPost]
        public JsonResult CancelMyOrder()
        {
            try
            {
                var body = ReadEntireRequestBody();
                if (string.IsNullOrWhiteSpace(body))
                    return Json(new { ok = false, error = "Missing payload." });

                long orderId = 0;
                long userId = 0;
                try
                {
                    var jo = JObject.Parse(body);
                    var oid = jo["orderId"] ?? jo["OrderId"];
                    var uid = jo["userId"] ?? jo["UserId"];
                    if (oid != null && oid.Type != JTokenType.Null)
                    {
                        if (oid.Type == JTokenType.Integer || oid.Type == JTokenType.Float) orderId = oid.Value<long>();
                        else long.TryParse(oid.ToString().Trim(), out orderId);
                    }
                    if (uid != null && uid.Type != JTokenType.Null)
                    {
                        if (uid.Type == JTokenType.Integer || uid.Type == JTokenType.Float) userId = uid.Value<long>();
                        else long.TryParse(uid.ToString().Trim(), out userId);
                    }
                }
                catch
                {
                    return Json(new { ok = false, error = "Invalid JSON." });
                }

                if (orderId <= 0 || userId <= 0)
                    return Json(new { ok = false, error = "Invalid request." });

                using (var con = OpenConn())
                {
                    EnsureOrderStatusSchema(con);
                    using (var cmd = new MySqlCommand(@"
                        UPDATE orders
                        SET status='cancelled'
                        WHERE id=@oid AND user_id=@uid
                          AND LOWER(TRIM(status)) IN ('pending','processing');", con))
                    {
                        cmd.Parameters.AddWithValue("@oid", orderId);
                        cmd.Parameters.AddWithValue("@uid", userId);
                        var rows = cmd.ExecuteNonQuery();
                        if (rows <= 0)
                            return Json(new { ok = false, error = "Cannot cancel this order (already shipped/delivered/cancelled, or not yours)." });
                    }
                    return Json(new { ok = true });
                }
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult PlaceOrder()
        {
            PlaceOrderDto dto = null;
            try
            {
                string body;
                using (var reader = new StreamReader(Request.InputStream, Encoding.UTF8))
                    body = reader.ReadToEnd();
                if (string.IsNullOrWhiteSpace(body))
                    return Json(new { ok = false, error = "Missing order data." });

                dto = JsonConvert.DeserializeObject<PlaceOrderDto>(body,
                    new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Ignore });
            }
            catch
            {
                return Json(new { ok = false, error = "Invalid order data format." });
            }

            try
            {
                if (dto == null || dto.UserId <= 0) return Json(new { ok = false, error = "Missing user." });
                if (dto.Items == null || dto.Items.Length == 0) return Json(new { ok = false, error = "Cart is empty." });

                // sanitize
                var items = dto.Items
                    .Where(i => i != null && i.BuildId > 0 && i.Quantity > 0 && i.UnitPrice >= 0)
                    .ToList();
                if (items.Count == 0) return Json(new { ok = false, error = "No valid items." });

                decimal subtotal = 0m;
                foreach (var it in items) subtotal += (it.UnitPrice * it.Quantity);

                // mirror checkout UI: shipping=500, tax=12%
                var shipping = 500m;
                var tax = Math.Round(subtotal * 0.12m, 0, MidpointRounding.AwayFromZero);
                var total = subtotal + shipping + tax;

                using (var con = OpenConn())
                using (var tx = con.BeginTransaction())
                {
                    var orderFkCol = ResolveOrderItemsOrderFkColumn(con);
                    var buildCol = ResolveOrderItemsBuildColumn(con);
                    var qtyCol = ResolveOrderItemsQtyColumn(con);
                    var priceCol = ResolveOrderItemsUnitPriceColumn(con);

                    if (string.IsNullOrEmpty(orderFkCol) || string.IsNullOrEmpty(buildCol) ||
                        string.IsNullOrEmpty(qtyCol) || string.IsNullOrEmpty(priceCol))
                    {
                        return Json(new
                        {
                            ok = false,
                            error = "Cannot place order: order_items columns do not match the app (need order FK, build id, quantity/qty, and unit price)."
                        });
                    }

                    long orderId;
                    using (var cmd = new MySqlCommand(@"INSERT INTO orders (user_id, total_amount, status, created_at)
                                                        VALUES (@uid, @total, 'pending', UTC_TIMESTAMP());
                                                        SELECT LAST_INSERT_ID();", con, tx))
                    {
                        cmd.Parameters.AddWithValue("@uid", dto.UserId);
                        cmd.Parameters.AddWithValue("@total", total);
                        orderId = Convert.ToInt64(cmd.ExecuteScalar());
                    }

                    foreach (var it in items)
                    {
                        var sql = string.Format(@"INSERT INTO order_items (`{0}`, `{1}`, `{2}`, `{3}`)
                                                           VALUES (@oid, @bid, @q, @p);",
                            orderFkCol, buildCol, qtyCol, priceCol);
                        using (var cmd = new MySqlCommand(sql, con, tx))
                        {
                            cmd.Parameters.AddWithValue("@oid", orderId);
                            cmd.Parameters.AddWithValue("@bid", it.BuildId);
                            cmd.Parameters.AddWithValue("@q", it.Quantity);
                            cmd.Parameters.AddWithValue("@p", it.UnitPrice);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    tx.Commit();
                    return Json(new { ok = true, orderId });
                }
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message });
            }
        }

        // ─── Users ────────────────────────────────────────────────────────────

        [HttpPost]
        public JsonResult ChangeUserRole()
        {
            var dto = ReadRequestJson<RoleChangeDto>();
            try
            {
                if (dto == null || dto.Id <= 0) return Json(new { ok = false, error = "Invalid user." });
                using (var con = OpenConn())
                {
                    var role = dto.Role == "admin" ? "admin" : "user";
                    using (var cmd = new MySqlCommand("UPDATE users SET role=@r WHERE id=@id", con))
                    {
                        cmd.Parameters.AddWithValue("@id", dto.Id);
                        cmd.Parameters.AddWithValue("@r", role);
                        var rows = cmd.ExecuteNonQuery();
                        if (rows <= 0) return Json(new { ok = false, error = "User not found." });
                    }
                    return Json(new { ok = true });
                }
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message });
            }
        }

        private sealed class DashboardSnapshot
        {
            public int Builds;
            public int Orders;
            public int Users;
            public decimal Revenue;
            public int Pending;
        }

        private static DashboardSnapshot LoadDashboardSnapshot(MySqlConnection con)
        {
            EnsureOrderStatusSchema(con);
            var s = new DashboardSnapshot();
            s.Builds = Convert.ToInt32(new MySqlCommand("SELECT COUNT(*) FROM pc_builds;", con).ExecuteScalar());
            s.Orders = Convert.ToInt32(new MySqlCommand("SELECT COUNT(*) FROM orders;", con).ExecuteScalar());
            s.Users = Convert.ToInt32(new MySqlCommand("SELECT COUNT(*) FROM users;", con).ExecuteScalar());
            using (var cmd = new MySqlCommand("SELECT IFNULL(SUM(total_amount),0) FROM orders WHERE status='delivered';", con))
                s.Revenue = Convert.ToDecimal(cmd.ExecuteScalar());
            s.Pending = Convert.ToInt32(new MySqlCommand("SELECT COUNT(*) FROM orders WHERE status='pending';", con).ExecuteScalar());
            return s;
        }

        private static void EnsureAdminDashboardCardsTable(MySqlConnection con)
        {
            using (var cmd = new MySqlCommand(@"
CREATE TABLE IF NOT EXISTS admin_dashboard_cards (
  id INT NOT NULL AUTO_INCREMENT,
  sort_order INT NOT NULL DEFAULT 0,
  title VARCHAR(160) NOT NULL,
  subtitle VARCHAR(220) NULL,
  accent VARCHAR(120) NULL,
  metric_key VARCHAR(40) NOT NULL,
  literal_value VARCHAR(220) NULL,
  is_active TINYINT(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (id),
  KEY ix_sort (sort_order, id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;", con))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private static void SeedDefaultDashboardCardsIfEmpty(MySqlConnection con)
        {
            var n = Convert.ToInt32(new MySqlCommand("SELECT COUNT(*) FROM admin_dashboard_cards;", con).ExecuteScalar());
            if (n > 0) return;
            using (var cmd = new MySqlCommand(@"
INSERT INTO admin_dashboard_cards (sort_order,title,subtitle,accent,metric_key,literal_value,is_active) VALUES
 (0,'Total builds','In catalog','rgba(120,255,180,0.4)','builds',NULL,1),
 (1,'Total orders','All time','rgba(80,180,255,0.4)','orders',NULL,1),
 (2,'Registered users','Including admins','rgba(255,200,80,0.4)','users',NULL,1),
 (3,'Revenue (delivered)','PHP (est.)','rgba(180,120,255,0.4)','revenue',NULL,1),
 (4,'Pending orders','Needs action','rgba(255,120,120,0.35)','pending',NULL,1);
", con))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private static bool IsValidMetricKey(string key)
        {
            switch ((key ?? "").Trim().ToLowerInvariant())
            {
                case "builds":
                case "orders":
                case "users":
                case "revenue":
                case "pending":
                case "literal":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Reads POST body reliably (InputStream may be non-seekable or partially consumed).</summary>
        private string ReadEntireRequestBody()
        {
            Stream stream;
            try
            {
                stream = Request.GetBufferedInputStream();
            }
            catch
            {
                stream = Request.InputStream;
            }

            try
            {
                if (stream != null && stream.CanSeek)
                    stream.Position = 0;
            }
            catch { /* non-seekable stream */ }

            using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true))
                return reader.ReadToEnd() ?? "";
        }

        /// <summary>Angular $http posts JSON; MVC5 does not bind JSON to action parameters by default.</summary>
        private T ReadRequestJson<T>() where T : class
        {
            var body = ReadEntireRequestBody();
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                return JsonConvert.DeserializeObject<T>(body,
                    new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Ignore });
            }
            catch
            {
                return null;
            }
        }

        private static string FormatDashboardCardValue(DashboardSnapshot snap, string metricKey, string literal)
        {
            var k = (metricKey ?? "").Trim().ToLowerInvariant();
            if (k == "literal") return literal ?? "";
            if (k == "builds") return snap.Builds.ToString();
            if (k == "orders") return snap.Orders.ToString();
            if (k == "users") return snap.Users.ToString();
            if (k == "revenue") return snap.Revenue.ToString("N0");
            if (k == "pending") return snap.Pending.ToString();
            return literal ?? "";
        }

        private static string Sha256Hex(string input)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input ?? ""));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}


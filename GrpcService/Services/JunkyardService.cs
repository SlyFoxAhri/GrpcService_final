using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;


namespace GrpcService.Services
{
    public class JunkyardService : Service.ServiceBase
    {
        private readonly MySqlDataSource _db;
        public JunkyardService(MySqlDataSource db)
        {
            _db = db;
        }

        //ADD USER INTO DATABASE - FOR TESTING
        private async void AddUser(string password)
        {
            string hashedpassword = BCrypt.Net.BCrypt.HashPassword(password);
            hashedpassword = hashedpassword;
            await using var connection = await _db.OpenConnectionAsync();
            string sql = "UPDATE user SET password = @password WHERE name = @name ";
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@name", "user321");
            command.Parameters.AddWithValue("@password", hashedpassword);
            command.ExecuteNonQueryAsync();
        }

        private async Task<bool> Islogin(string sessionId)
        {
            await using var connection = await _db.OpenConnectionAsync();
            string sql = "SELECT id FROM user WHERE id = @id;";
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", sessionId);
            
            var reasult = await command.ExecuteScalarAsync();
            return reasult != null;
        }
        private async Task<bool> IsExists(int Id)
        {
            await using var connection = await _db.OpenConnectionAsync();
            string sql = "SELECT id FROM yards WHERE id = @id;";
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", Id);

            var resoult = await command.ExecuteScalarAsync();
            return resoult != null;
        }

        private bool Validate(Yard req)
        {
            if (req.Id <= 0) return false;
            if (req.District == "0" || req.District.Length > 3) return false;
            if (string.IsNullOrWhiteSpace(req.Address) || req.Address.Length > 40) return false;
            return true;
        }

        private string GenerateJwt(string username)
        {
            var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: "JunkyardServer",
                audience: "JunkyardClient",
                claims: new[] { new Claim(ClaimTypes.Name, username) },
                expires: DateTime.UtcNow.AddMinutes(60),
                signingCredentials: creds
            );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        //LOGIN
        public override async Task<SessionId> Login(User req, ServerCallContext context)
        {
            //AddUser("passwd");
            if(string.IsNullOrWhiteSpace(req.Name) || req.Name.Length > 50 || string.IsNullOrWhiteSpace(req.Password) || req.Password.Length > 100)
                return new SessionId { Id = "", Jwtoken = "" };
             
            await using var connection = await _db.OpenConnectionAsync();
            string sql = "SELECT password FROM user WHERE name = @name;";
            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@name", req.Name);

            var result = await command.ExecuteScalarAsync() as string;
            if (result == null || !BCrypt.Net.BCrypt.Verify(req.Password, result))
                return new SessionId { Id = "", Jwtoken = "" };

            string temp = Guid.NewGuid().ToString();

            string sql1 = "UPDATE user SET id = @id WHERE name = @name ;";
            await using var command1 = new MySqlCommand(sql1, connection);
            command1.Parameters.AddWithValue("@id", temp);
            command1.Parameters.AddWithValue("@name", req.Name);
            command1.ExecuteNonQueryAsync();

            var token = GenerateJwt(req.Name);
            return new SessionId { Id=temp, Jwtoken = token };

            
        }

        
        //CREATE
        [Authorize]
        public async override Task<Resoult> Create(Yard req, ServerCallContext context)
        {
            if (!Validate(req))
                return new Resoult { Success = "Invalid input, try again :<" };
            if (!await Islogin(req.Sessionid)) { return new Resoult { Success = "Log in!! >:c" }; }
            if (await IsExists(req.Id)) { return new Resoult { Success = "Id already exist" }; }

            await using var connection = await _db.OpenConnectionAsync();

            string sql1 = "INSERT INTO yards(id, district, address) VALUES(@id, @district, @address);";
            string sql2 = "INSERT INTO collects(yardid) VALUES(@id);";
            string sql3 = "UPDATE collects SET typeid=(select id from types where wname = @wname) WHERE yardid = @id;" ;

            await using var command1 = new MySqlCommand(sql1, connection);
            await using var command2 = new MySqlCommand(sql2, connection);
            await using var command3 = new MySqlCommand(sql3, connection);

            command1.Parameters.AddWithValue("@id", req.Id);
            command1.Parameters.AddWithValue("@district", req.District);
            command1.Parameters.AddWithValue("@address", req.Address);
            command2.Parameters.AddWithValue("@id", req.Id);
            command3.Parameters.AddWithValue("@id", req.Id);
            command3.Parameters.AddWithValue("@wname", req.Waste);

            await command1.ExecuteNonQueryAsync();
            await command2.ExecuteNonQueryAsync();
            await command3.ExecuteNonQueryAsync();

            return new Resoult { Success = "Success :3" };
        }

        //READ
        public async override Task<YardList> Read(Empty req, ServerCallContext context)
        {
            var result = new YardList();
            string sql = "SELECT id, district, address FROM yards";

            await using var connection = await _db.OpenConnectionAsync();
            await using var command = new MySqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var yard = new NewYard
                {
                    Id = reader.GetInt32("id"),
                    District = reader.GetString("district"),
                    Address = reader.GetString("address"),
                };

                result.Yards.Add(yard);
            }
            return result;
        }

        //UPDATE
        [Authorize]
        public async override Task<Resoult> Update(Yard req, ServerCallContext context)
        {
            if (!Validate(req))
                return new Resoult { Success = "Invalid input, try again :<" };
            if (!await IsExists(req.Id)) { return new Resoult { Success = "Id doesn't exist" }; }

            await using var connection = await _db.OpenConnectionAsync();

            string sql1 = "UPDATE yards SET district = @district, address = @address WHERE id = @id;";
            string sql2 = "UPDATE collects SET yardid = @id, typeid =(SELECT id FROM types where wname = @wname);";

            await using var command1 = new MySqlCommand(sql1, connection);
            await using var command2 = new MySqlCommand(sql2, connection);

            command1.Parameters.AddWithValue("@district", req.District);
            command1.Parameters.AddWithValue("@address", req.Address);
            command1.Parameters.AddWithValue("@id", req.Id);
            command2.Parameters.AddWithValue("@id", req.Id);
            command2.Parameters.AddWithValue("@wname", req.Waste);
            

            int rows = await command1.ExecuteNonQueryAsync();
            await command2.ExecuteNonQueryAsync();
            if (rows > 0) { return new Resoult { Success = "Success :3" }; }

            return new Resoult { Success = "Something went wrong :/ " };
        }

        //DELETE
        [Authorize]
        public async override Task<Resoult> Delete(Yard req, ServerCallContext context)
        {
            if (!Validate(req))
                return new Resoult { Success = "Invalid input, try again :<" };
            if (!await IsExists(req.Id)) { return new Resoult { Success = "Id doesn't exist" }; }


            await using var connection = await _db.OpenConnectionAsync();

            string sql1 = "DELETE FROM yards WHERE id = @id;";
            string sql2 = "DELETE FROM collects WHERE yardid = @id;";

            await using var command1 = new MySqlCommand(sql1, connection);
            await using var command2 = new MySqlCommand(sql2, connection);

            command1.Parameters.AddWithValue("@id", req.Id);
            command2.Parameters.AddWithValue("@id", req.Id);

            await command2.ExecuteNonQueryAsync();
            int rows = await command1.ExecuteNonQueryAsync();
            if (rows > 0) { return new Resoult { Success = "Success :3" }; }

            return new Resoult { Success = "Something went wrong :/ " };
        }

        //QUERY 1
        public async override Task<BPCount> BudaPestCount(Empty req, ServerCallContext context)
        {
            int buda = 0;
            int pest = 0;
            List<string> buda_list = ["1", "2", "3", "5", "6", "22"];

            await using var connection = await _db.OpenConnectionAsync();

            string sql = "SELECT district FROM yards ";

            await using var command = new MySqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                if (buda_list.Contains(reader.GetString("district")))
                    buda++;
                else
                    pest++;
            }

            return new BPCount()
            {
                BudaCount = buda,
                PestCount = pest
            };
        }

        //QUERY 2
        public async override Task<YardList> SeveralYards(Empty req, ServerCallContext context)
        {
            var resoult = new YardList();
            await using var connection = await _db.OpenConnectionAsync();

            string sql = "SELECT district, COUNT(*) AS count FROM yards GROUP BY district HAVING count > 1;";

            await using var command = new MySqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while(await reader.ReadAsync())
            {
                var yard = new NewYard
                {                    
                    District = reader.GetString("district"),
                };
                resoult.Yards.Add(yard);
            }
            return resoult;
        }

        //QUERY 3
        public async override Task<YardList> WasteType(Count req, ServerCallContext context)
        {
            var resoult = new YardList();
            await using var connection = await _db.OpenConnectionAsync();

            string sql = "SELECT t.wname FROM types t JOIN collects c ON t.id = c.typeId GROUP BY t.id, t.wname HAVING COUNT(DISTINCT c.yardId) < @max;";
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@max", req.Count_);
            await using var reader = await command.ExecuteReaderAsync();

            while(await reader.ReadAsync())
            {
                var yard = new NewYard
                {
                    Waste = reader.GetString("wname")
                };
                resoult.Yards.Add(yard);
            }
            return resoult;
        }
    }
}

using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity.Data;
using MySqlConnector;
using System.Collections.Generic;
using System.Net;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace GrpcService.Services
{
    public class JunkyardService : Service.ServiceBase
    {
        static Dictionary<string, string> users = new() { { "user", "password" } };
        static List<string> sessions = new();
        static List<int> id = new();
        private readonly MySqlDataSource _db;
        public JunkyardService(MySqlDataSource db)
        {
            _db = db;
        }
        private bool IsLoggedIn(string sessionId)
        {
            lock (sessions)
                return sessions.Contains(sessionId);
        }


        //LOGIN
        public override Task<SessionId> Login(User req, ServerCallContext context)
        {
            string id = "";
            if (users.TryGetValue(req.Name, out var passwd))
            {
                if (req.Password == passwd)
                {
                    id = Guid.NewGuid().ToString();
                    lock (sessions)
                        sessions.Add(id);
                    return Task.FromResult(new SessionId { Id = id });
                }
            }
            return Task.FromResult(new SessionId { Id = "" });
        }

        //LOGOUT
        public override Task<Resoult> Logout(SessionId req, ServerCallContext context)
        {
            lock (sessions)
            {
                if (!sessions.Contains(req.Id))
                {
                    sessions.Remove(req.Id);
                    return Task.FromResult(new Resoult { Success = "Logged out :3" });
                }
                else
                    return Task.FromResult(new Resoult { Success = "Already logged out" });
            }
        }

        //CREATE
        public async override Task<Resoult> Create(Yard req, ServerCallContext context)
        {
            if (!IsLoggedIn(req.Sessionid)) { return new Resoult { Success = "Log in!! >:c" }; }
            if (id.Contains(req.Id)) { return new Resoult { Success = "Id already exist" }; }

            id.Add(req.Id);
            await using var connection = await _db.OpenConnectionAsync();

            string sql1 = "INSERT INTO yards(id, district, address) VALUES(@id, @district, @address);";
            //string sql2 = "INSERT INTO waste(wname) VALUES(@wname);";  

            await using var command1 = new MySqlCommand(sql1, connection);
            //await using var command2 = new MySqlCommand(sql2, connection);

            command1.Parameters.AddWithValue("@id", req.Id);
            command1.Parameters.AddWithValue("@district", req.District);
            command1.Parameters.AddWithValue("@address", req.Address);
            //command2.Parameters.AddWithValue("@waste", req.Address);

            await command1.ExecuteNonQueryAsync();
            //await command2.ExecuteNonQueryAsync(); 

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
                    Address = reader.GetString("address")
                };

                result.Yards.Add(yard);
            }
            return result;
        }

        //UPDATE
        public async override Task<Resoult> Update(Yard req, ServerCallContext context)
        {
            if (!IsLoggedIn(req.Sessionid)) { return new Resoult { Success = "Log in!! >:c" }; }
            if (!id.Contains(req.Id)) { return new Resoult { Success = "Id doesn't exist" }; }

            await using var connection = await _db.OpenConnectionAsync();

            string sql = "UPDATE yards SET district = @district, address = @address WHERE id = @id;";

            await using var command = new MySqlCommand(sql, connection);

            command.Parameters.AddWithValue("@district", req.District);
            command.Parameters.AddWithValue("@address", req.Address);
            command.Parameters.AddWithValue("@id", req.Id);

            int rows = await command.ExecuteNonQueryAsync();
            if (rows > 0) { return new Resoult { Success = "Success :3" }; }

            return new Resoult { Success = "Something went wrong :/ " };
        }

        //DELETE
        public async override Task<Resoult> Delete(Yard req, ServerCallContext context)
        {
            if (!IsLoggedIn(req.Sessionid)) { return new Resoult { Success = "Log in!! >:c" }; }
            if (!id.Contains(req.Id)) { return new Resoult { Success = "Id doesn't exist" }; }

            id.Remove(req.Id);
            await using var connection = await _db.OpenConnectionAsync();

            string sql = "DELETE FROM yards WHERE id = @id;";

            await using var command = new MySqlCommand(sql, connection);

            command.Parameters.AddWithValue("@district", req.District);
            command.Parameters.AddWithValue("@address", req.Address);

            int rows = await command.ExecuteNonQueryAsync();
            if (rows > 0) { return new Resoult { Success = "Success :3" }; }

            return new Resoult { Success = "Something went wrong :/ " };
        }

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
                    Id = 0,
                    District = reader.GetString("district"),
                    Address = ""
                };
                resoult.Yards.Add(yard);
            }
            return resoult;
        }
    }
}

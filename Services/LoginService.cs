﻿using bazyProjektBlazor.Auth;
using bazyProjektBlazor.Requests;
using bazyProjektBlazor.Utilities;
using MySqlConnector;

namespace bazyProjektBlazor.Services
{
    public interface ILoginService
    {
        public Task<bool> Login(LoginRequest loginModel);

        public Task<bool> IsLogged();

        public Task Logout();

        public Task<string> GetRoles();
    }
    public class LoginService(IConfiguration configuration, ICurrentUser currentUser) : ILoginService
    {
        public Task<string> GetRoles()
        {
            return Task.FromResult(currentUser.Roles);
        }

        public async Task<bool> IsLogged()
        {
            if (currentUser.ID == -1)
                return await Task.FromResult(false);
            return await Task.FromResult(true);
        }

        public async Task<bool> Login(LoginRequest request)
        {
            int id = -1;
            string roles = "user";
            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));
            await connection.OpenAsync();

            var queryIsInDatabase = "SELECT ID, isAdmin FROM users WHERE email=@email AND password=@password";
            using var commandIsInDatabase = new MySqlCommand(queryIsInDatabase, connection);
            commandIsInDatabase.Parameters.AddWithValue("@email", request.Email);
            commandIsInDatabase.Parameters.AddWithValue("@password", Utility.EncryptSHA256(request.Password));

            using var readerIsInDatabase = await commandIsInDatabase.ExecuteReaderAsync();
            if (!readerIsInDatabase.HasRows)
                return await Task.FromResult(false);
            else
            {
                while (await readerIsInDatabase.ReadAsync())
                {
                    id = readerIsInDatabase.GetInt32(0);
                    if (readerIsInDatabase.GetBoolean(1))
                        roles += ",admin";
                }

                using (var directorConnection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection")))
                {
                    await directorConnection.OpenAsync();
                    using var commandIsDirector = new MySqlCommand("SELECT directorID FROM departments WHERE directorID=@directorID", directorConnection);
                    commandIsDirector.Parameters.AddWithValue("@directorID", id);
                    int rows = await commandIsDirector.ExecuteNonQueryAsync();
                    if (rows > 0)
                    {
                        roles += ",director";
                    }
                }

                using (var leaderConnection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection")))
                {
                    await leaderConnection.OpenAsync();
                    using var commandIsLeader = new MySqlCommand("SELECT teamsmembers.memberID FROM teamsmembers WHERE teamsmembers.memberID = @leaderID AND teamsmembers.isLeader = 1", leaderConnection);
                    commandIsLeader.Parameters.AddWithValue("@leaderID", id);
                    int rows = await commandIsLeader.ExecuteNonQueryAsync();
                    if (rows > 0)
                    {
                        roles += ",leader";
                    }

                }
                currentUser.ID = id;
                currentUser.Roles = roles;
                return await Task.FromResult(true);
            }
        }

        public Task Logout()
        {
            currentUser.ID = -1;
            currentUser.Roles = string.Empty;
            return Task.CompletedTask;
        }
    }


}

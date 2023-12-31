﻿using bazyProjektBlazor.Auth;
using bazyProjektBlazor.Models;
using MySqlConnector;

namespace bazyProjektBlazor.Services
{
    public interface ITeamsService
    {
        public Task<List<Team>> GetAllTeams();

        public Task<Team> GetMyTeam();

        public Task<Team> GetTeamByID(int id);

        public Task<List<User>> GetMembersLeader();

        public Task<List<User>> GetMembersMember();

    }
    public class TeamsService(IConfiguration configuration, ICurrentUser currentUser, IUsersService usersService) : ITeamsService
    {
        public async Task<List<Team>> GetAllTeams()
        {
            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            connection.Open();

            using var command = new MySqlCommand(
                "SELECT ID FROM teams", connection);

            MySqlDataReader reader = await command.ExecuteReaderAsync();

            List<Team> teams = [];

            while (await reader.ReadAsync())
            {
                Team team = await GetTeamByID(reader.GetInt32("ID"));
                teams.Add(team);
            }

            return await Task.FromResult(teams);
        }

        public async Task<List<User>> GetMembersLeader()
        {
            List<User> response = [];

            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            connection.Open();

            using var command = new MySqlCommand("SELECT teamsmembers.memberID FROM teamsmembers INNER JOIN teams ON teamsmembers.teamID = teams.ID WHERE teams.leaderID = @ID", connection);
            command.Parameters.AddWithValue("@ID", currentUser.ID);

            MySqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                response.Add(await usersService.GetUserById(reader.GetInt32("memberID")));
            }

            return await Task.FromResult(response);
        }

        public async Task<List<User>> GetMembersMember()
        {
            List<User> response = [];

            using var membersConnection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            membersConnection.Open();

            using var membersCommand = new MySqlCommand("SELECT teamsmembers.memberID FROM teamsmembers WHERE teamsmembers.teamID = (SELECT teamsmembers.teamID FROM teamsmembers WHERE teamsmembers.memberID = @ID) AND teamsmembers.memberID != @ID", membersConnection);
            membersCommand.Parameters.AddWithValue("@ID", currentUser.ID);

            MySqlDataReader reader = await membersCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                response.Add(await usersService.GetUserById(reader.GetInt32("memberID")));
            }

            using var leaderConnection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            leaderConnection.Open();

            using var leaderCommand = new MySqlCommand("SELECT teams.leaderID FROM teams INNER JOIN teamsmembers ON teams.ID = teamsmembers.teamID AND teamsmembers.memberID = @ID", leaderConnection);
            membersCommand.Parameters.AddWithValue("@ID", currentUser.ID);

            reader = await membersCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                response.Add(await usersService.GetUserById(reader.GetInt32("leaderID")));
            }

            return await Task.FromResult(response);
        }

        public async Task<Team> GetMyTeam()
        {
            Team response = new();
            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            connection.Open();

            using var command = new MySqlCommand("SELECT teams.ID FROM teams WHERE teams.leaderID=@ID", connection);

            command.Parameters.AddWithValue("@ID", currentUser.ID);

            MySqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                response = await GetTeamByID(reader.GetInt32("ID"));
            }

            return await Task.FromResult(response);

        }

        public async Task<Team> GetTeamByID(int id)
        {
            Team response = new();
            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            connection.Open();

            using var command = new MySqlCommand(
                "SELECT teams.ID, teams.name, teams.leaderID, " +
                "departments.name " +
                "FROM teams " +
                "INNER JOIN departments on teams.departmentID = departments.ID " +
                "WHERE teams.ID=@ID"
                , connection);

            command.Parameters.AddWithValue("@ID", id);

            MySqlDataReader reader = await command.ExecuteReaderAsync();
            List<User> members = [];
            while (await reader.ReadAsync())
            {

                response.ID = reader.GetInt32(0);
                response.Name = reader.GetString(1);
                response.Leader = await usersService.GetUserById(reader.GetInt32(2));
                response.DepartmentName = reader.GetString(3);

                using var membersConnection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));
                membersConnection.Open();

                using var membersCommand = new MySqlCommand("SELECT users.ID FROM users INNER JOIN teamsmembers ON teamsmembers.memberID = users.ID WHERE teamsmembers.teamID = @ID", membersConnection);
                membersCommand.Parameters.AddWithValue("@ID", id);
                MySqlDataReader membersReader = await membersCommand.ExecuteReaderAsync();

                while (await membersReader.ReadAsync())
                {
                    members.Add(await usersService.GetUserById(membersReader.GetInt32("ID")));
                }
            }
            response.Members = members;
            return await Task.FromResult(response);
        }
    }
}

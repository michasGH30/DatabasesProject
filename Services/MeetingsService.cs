﻿using bazyProjektBlazor.Auth;
using bazyProjektBlazor.Models;
using bazyProjektBlazor.Requests;
using bazyProjektBlazor.Responses;
using MySqlConnector;

namespace bazyProjektBlazor.Services
{
    public interface IMeetingsService
    {
        public Task<List<MeetingSummaryResponse>> GetAllMeetingsSummaries();

        public Task<MeetingSummaryResponse> GetMeetingSummaryByID(int ID);

        public Task<List<MeetingSummaryResponse>> GetMeetingsSummariesFromMyDepartment();

        public Task<List<MeetingSummaryResponse>> GetMeetingsSummariesFromMyTeam();

        public Task<Meeting> GetMeetingByID(int id);

        public Task<bool> CreateMeeting(CreateMeetingRequest request);

        public Task<List<MeetingSummaryResponse>> GetMyMeetingSummaries();

        public Task<bool> DeleteMeetingByID(int id);

        public Task<List<TypeStatusRepetitionOfMeeting>> GetRepetitionOfMeeting();

        public Task<List<TypeStatusRepetitionOfMeeting>> GetTypesOfMeeting();

        public Task<List<TypeStatusRepetitionOfMeeting>> GetStatusesOfMeeting();

        public Task<bool> UpdateMeeting(CreateMeetingRequest request);
    }
    public class MeetingsService(IConfiguration configuration, ICurrentUser currentUser, IUsersService usersService) : IMeetingsService
    {
        public async Task<bool> CreateMeeting(CreateMeetingRequest request)
        {
            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            connection.Open();

            using var command = new MySqlCommand("INSERT INTO meetings(title, date, creatorID, typeID, statusID, repeatingID) VALUES (@TITLE,@DATE,@CID,@TID,@SID,@RID)", connection);
            command.Parameters.AddWithValue("@TITLE", request.Title);
            command.Parameters.AddWithValue("@DATE", request.Date);
            command.Parameters.AddWithValue("@CID", currentUser.ID);
            command.Parameters.AddWithValue("@TID", request.TypeOfMeeting);
            command.Parameters.AddWithValue("@SID", 1);
            command.Parameters.AddWithValue("@RID", request.RepetitionOfMeeting);

            if (command.ExecuteNonQuery() > 0)
            {

                if (request.MembersID.Count > 0)
                {
                    var lastID = command.LastInsertedId;

                    string sql = "INSERT INTO meetingsmembers(meetingID,memberID) VALUES ";
                    for (int i = 0; i < request.MembersID.Count; i++)
                    {
                        if (i != request.MembersID.Count - 1)
                        {
                            sql += $"({lastID}, {request.MembersID.ElementAt(i)}), ";
                        }
                        else
                        {
                            sql += $"({lastID}, {request.MembersID.ElementAt(i)})";
                        }
                    }
                    using var membersConnetion = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

                    membersConnetion.Open();

                    using var membersCommand = new MySqlCommand(sql, connection);

                    if (membersCommand.ExecuteNonQuery() > 0)
                    {
                        return await Task.FromResult(true);
                    }
                    else
                    {
                        return await Task.FromResult(false);
                    }
                }

                return await Task.FromResult(true);

            }
            else
            {
                return await Task.FromResult(false);
            }

        }

        public async Task<bool> DeleteMeetingByID(int id)
        {
            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            connection.Open();

            using var command = new MySqlCommand("DELETE FROM meetings WHERE meetings.ID = @ID", connection);
            command.Parameters.AddWithValue("@ID", id);

            if (command.ExecuteNonQuery() > 0)
            {
                return await Task.FromResult(true);
            }
            else
            {
                return await Task.FromResult(false);
            }
        }

        public async Task<List<MeetingSummaryResponse>> GetAllMeetingsSummaries()
        {
            List<MeetingSummaryResponse> response = [];

            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            connection.Open();

            using var command = new MySqlCommand("SELECT meetings.ID FROM meetings", connection);

            MySqlDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                MeetingSummaryResponse meeting = await GetMeetingSummaryByID(reader.GetInt32(0));
                response.Add(meeting);
            }

            return await Task.FromResult(response);
        }

        public async Task<Meeting> GetMeetingByID(int id)
        {
            Meeting response = new();
            using var meetingConnection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            meetingConnection.Open();

            using var meetingCommand = new MySqlCommand("SELECT meetings.ID, meetings.title, meetings.date, typeofmeeting.type, repetitionofmeeting.repetition, statusofmeeting.status, users.ID FROM meetings INNER JOIN typeofmeeting on meetings.typeID = typeofmeeting.ID INNER JOIN repetitionofmeeting on meetings.repeatingID = repetitionofmeeting.ID INNER JOIN statusofmeeting on meetings.statusID = statusofmeeting.ID INNER JOIN users on meetings.creatorID = users.ID WHERE meetings.ID = @ID;", meetingConnection);
            meetingCommand.Parameters.AddWithValue("@ID", id);

            MySqlDataReader meetingsReader = meetingCommand.ExecuteReader();

            while (meetingsReader.Read())
            {
                response.ID = meetingsReader.GetInt32(0);
                response.Title = meetingsReader.GetString(1);
                response.Date = meetingsReader.GetDateOnly(2);
                response.TypeOfMeeting = meetingsReader.GetString(3);
                response.RepetitionOfMeeting = meetingsReader.GetString(4);
                response.StatusOfMeeting = meetingsReader.GetString(5);
                response.Creator = await usersService.GetUserById(meetingsReader.GetInt32(6));
            }

            List<User> members = [];

            using var membersConnection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));
            membersConnection.Open();

            using var membersCommand = new MySqlCommand("SELECT meetingsmembers.memberID FROM meetingsmembers WHERE meetingsmembers.meetingID = @ID", membersConnection);
            membersCommand.Parameters.AddWithValue("@ID", id);

            MySqlDataReader membersReader = membersCommand.ExecuteReader();

            while (membersReader.Read())
            {
                User member = await usersService.GetUserById(membersReader.GetInt32(0));
                members.Add(member);
            }

            List<MeetingMessage> messages = [];

            using var messagesConnection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            messagesConnection.Open();

            using var messagesCommand = new MySqlCommand("SELECT meetingschats.ID, meetingschats.message, meetingschats.senderID FROM meetingschats WHERE meetingschats.meetingID = @ID", messagesConnection);
            messagesCommand.Parameters.AddWithValue("@ID", id);

            MySqlDataReader messagesReader = messagesCommand.ExecuteReader();

            while (messagesReader.Read())
            {
                messages.Add(new()
                {
                    ID = messagesReader.GetInt32(0),
                    Message = messagesReader.GetString(1),
                    Sender = await usersService.GetUserById(messagesReader.GetInt32(2))
                });
            }

            List<MeetingAttachment> attachments = [];

            using var attachmentsConnection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            attachmentsConnection.Open();

            using var attachmentsCommand = new MySqlCommand("SELECT meetingattachments.ID, meetingattachments.name, typesofattachments.type, users.ID FROM meetingattachments INNER JOIN typesofattachments on meetingattachments.typeID = typesofattachments.ID INNER JOIN users ON meetingattachments.senderID = users.ID WHERE meetingattachments.meetingID = @ID", attachmentsConnection);

            attachmentsCommand.Parameters.AddWithValue("@ID", id);

            MySqlDataReader attachmentsReader = attachmentsCommand.ExecuteReader();

            while (attachmentsReader.Read())
            {
                attachments.Add(new()
                {
                    ID = attachmentsReader.GetInt32(0),
                    Name = attachmentsReader.GetString(1),
                    Type = attachmentsReader.GetString(2),
                    Sender = await usersService.GetUserById(attachmentsReader.GetInt32(3))
                });
            }

            response.Members = members;
            response.Messages = messages;
            response.Attachments = attachments;

            return await Task.FromResult(response);
        }

        public async Task<List<MeetingSummaryResponse>> GetMeetingsSummariesFromMyDepartment()
        {
            List<MeetingSummaryResponse> response = [];

            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            connection.Open();

            using var command = new MySqlCommand("SELECT meetings.ID FROM meetings INNER JOIN meetingsmembers ON meetings.ID = meetingsmembers.meetingID WHERE meetings.creatorID IN (SELECT teams.leaderID FROM teams WHERE teams.departmentID = @ID) OR meetingsmembers.memberID IN (SELECT teamsmembers.memberID FROM teamsmembers INNER JOIN teams ON teamsmembers.teamID = teams.ID WHERE teams.departmentID = @ID)", connection);
            command.Parameters.AddWithValue("@ID", currentUser.ID);

            MySqlDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                MeetingSummaryResponse meeting = await GetMeetingSummaryByID(reader.GetInt32(0));
                response.Add(meeting);
            }

            return await Task.FromResult(response);
        }

        public async Task<List<MeetingSummaryResponse>> GetMeetingsSummariesFromMyTeam()
        {
            List<MeetingSummaryResponse> response = [];

            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            connection.Open();

            using var command = new MySqlCommand("SELECT meetings.ID FROM meetings INNER JOIN meetingsmembers ON meetings.ID = meetingsmembers.meetingID WHERE meetings.creatorID IN (SELECT teamsmembers.memberID FROM teamsmembers INNER JOIN teams ON teamsmembers.teamID = teams.ID WHERE teams.leaderID = @ID) OR meetingsmembers.memberID IN (SELECT teamsmembers.memberID FROM teamsmembers INNER JOIN teams ON teamsmembers.teamID = teams.ID WHERE teams.leaderID = @ID)", connection);
            command.Parameters.AddWithValue("@ID", currentUser.ID);

            MySqlDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                MeetingSummaryResponse meeting = await GetMeetingSummaryByID(reader.GetInt32(0));
                response.Add(meeting);
            }

            return await Task.FromResult(response);
        }

        public async Task<MeetingSummaryResponse> GetMeetingSummaryByID(int ID)
        {
            MeetingSummaryResponse response = new();

            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            connection.Open();

            using var command = new MySqlCommand("SELECT meetings.ID, meetings.title, meetings.date, meetings.creatorID, statusofmeeting.status FROM meetings INNER JOIN statusofmeeting on meetings.statusID = statusofmeeting.ID WHERE meetings.ID=@ID", connection);
            command.Parameters.AddWithValue("@ID", ID);

            MySqlDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                response = new()
                {
                    MeetingID = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    Date = reader.GetDateOnly(2),
                    Creator = reader.GetInt32(3) == currentUser.ID,
                    Status = reader.GetString(4)
                };
            }

            return await Task.FromResult(response);
        }

        public async Task<List<MeetingSummaryResponse>> GetMyMeetingSummaries()
        {
            List<MeetingSummaryResponse> response = [];

            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            connection.Open();

            using var command = new MySqlCommand("SELECT meetings.ID FROM meetings INNER JOIN statusofmeeting ON meetings.statusID = statusofmeeting.ID WHERE meetings.creatorID = @ID OR meetings.ID IN (SELECT meetingsmembers.meetingID FROM meetingsmembers WHERE meetingsmembers.memberID = @ID)", connection);
            command.Parameters.AddWithValue("@ID", currentUser.ID);

            MySqlDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                MeetingSummaryResponse r = await GetMeetingSummaryByID(reader.GetInt32(0));
                response.Add(r);
            }

            return await Task.FromResult(response);
        }

        public async Task<List<TypeStatusRepetitionOfMeeting>> GetRepetitionOfMeeting()
        {
            List<TypeStatusRepetitionOfMeeting> response = [];

            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            connection.Open();

            using var command = new MySqlCommand("SELECT repetitionofmeeting.ID, repetitionofmeeting.repetition FROM repetitionofmeeting", connection);

            MySqlDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                response.Add(new()
                {
                    ID = reader.GetInt32(0),
                    Name = reader.GetString(1)
                });
            }

            return await Task.FromResult(response);
        }

        public async Task<List<TypeStatusRepetitionOfMeeting>> GetStatusesOfMeeting()
        {
            List<TypeStatusRepetitionOfMeeting> response = [];

            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            connection.Open();

            using var command = new MySqlCommand("SELECT statusofmeeting.ID, statusofmeeting.status FROM statusofmeeting", connection);

            MySqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                response.Add(new()
                {
                    ID = reader.GetInt32(0),
                    Name = reader.GetString(1)
                });
            }

            return await Task.FromResult(response);
        }

        public async Task<List<TypeStatusRepetitionOfMeeting>> GetTypesOfMeeting()
        {
            List<TypeStatusRepetitionOfMeeting> response = [];

            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            connection.Open();

            using var command = new MySqlCommand("SELECT typeofmeeting.ID, typeofmeeting.type FROM typeofmeeting", connection);

            MySqlDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                response.Add(new()
                {
                    ID = reader.GetInt32(0),
                    Name = reader.GetString(1)
                });
            }

            return await Task.FromResult(response);
        }

        public async Task<bool> UpdateMeeting(CreateMeetingRequest request)
        {
            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            connection.Open();

            using var command = new MySqlCommand("UPDATE meetings SET title=@T, date=@D, typeID=@TID, statusID=@SID, repeatingID=@RID WHERE ID=@ID", connection);

            command.Parameters.AddWithValue("@T", request.Title);
            command.Parameters.AddWithValue("@D", request.Date);
            command.Parameters.AddWithValue("@TID", request.TypeOfMeeting);
            command.Parameters.AddWithValue("@SID", request.StatusOfMeeting);
            command.Parameters.AddWithValue("@RID", request.RepetitionOfMeeting);
            command.Parameters.AddWithValue("@ID", request.ID);

            if (command.ExecuteNonQuery() > 0)
            {
                using var deleteConnection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

                deleteConnection.Open();

                using var deleteCommand = new MySqlCommand("DELETE FROM meetingsmembers WHERE meetingID=@ID", deleteConnection);
                deleteCommand.Parameters.AddWithValue("@ID", request.ID);
                if (deleteCommand.ExecuteNonQuery() > 0)
                {
                    if (request.MembersID.Count > 0)
                    {
                        using var insertConnection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

                        insertConnection.Open();

                        string sql = "INSERT INTO meetingsmembers(meetingID,memberID) VALUES ";
                        for (int i = 0; i < request.MembersID.Count; i++)
                        {
                            if (i != request.MembersID.Count - 1)
                            {
                                sql += $"({request.ID}, {request.MembersID.ElementAt(i)}), ";
                            }
                            else
                            {
                                sql += $"({request.ID}, {request.MembersID.ElementAt(i)})";
                            }
                        }

                        using var insertCommand = new MySqlCommand(sql, insertConnection);

                        if (insertCommand.ExecuteNonQuery() > 0)
                        {
                            return await Task.FromResult(true);
                        }
                        else
                        {
                            return await Task.FromResult(false);
                        }
                    }

                    return await Task.FromResult(true);

                }
                else
                {
                    return await Task.FromResult(false);
                }

            }
            else
            {
                return await Task.FromResult(false);
            }


        }
    }
}
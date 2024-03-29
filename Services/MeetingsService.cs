﻿using bazyProjektBlazor.Auth;
using bazyProjektBlazor.Models;
using bazyProjektBlazor.Requests;
using bazyProjektBlazor.Responses;
using MySqlConnector;

namespace bazyProjektBlazor.Services
{
    public interface IMeetingsService
    {
        public Task<List<MeetingSummaryResponse>?> GetAllMeetingsSummaries();

        public Task<MeetingSummaryResponse?> GetMeetingSummaryByID(int ID);

        public Task<List<MeetingSummaryResponse>?> GetMeetingsSummariesFromMyDepartment();

        public Task<List<MeetingSummaryResponse>?> GetMeetingsSummariesFromMyTeam();

        public Task<Meeting?> GetMeetingByID(int id);

        public Task<bool> CreateMeeting(CreateMeetingRequest request);

        public Task<List<MeetingSummaryResponse>?> GetMyMeetingSummaries();

        public Task<bool> DeleteMeetingByID(int id);

        public Task<List<TypeStatusRepetitionOfMeetingResponse>> GetRepetitionOfMeeting();

        public Task<List<TypeStatusRepetitionOfMeetingResponse>> GetTypesOfMeeting();

        public Task<List<TypeStatusRepetitionOfMeetingResponse>> GetStatusesOfMeeting();

        public Task<bool> UpdateMeeting(CreateMeetingRequest request);

        public Task<List<RoomResponse>> GetRooms();
    }
    public class MeetingsService(IConfiguration configuration, ICurrentUser currentUser, IUsersService usersService, IMessagesService messagesService, IAttachmentsService attachmentsService) : IMeetingsService
    {
        public async Task<bool> CreateMeeting(CreateMeetingRequest request)
        {
            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            await connection.OpenAsync();

            var transaction = connection.BeginTransaction();

            try
            {
                var sqlInsert = "";

                if (request.Description == null)
                {
                    if (request.IsOnline)
                    {
                        sqlInsert = "INSERT INTO meetings(title, date, isOnline, link, typeID, statusID, repeatingID) VALUES (@TITLE,@DATE,@ISONLINE,@LINK,@TID,@SID,@RID)";
                    }
                    else
                    {
                        sqlInsert = "INSERT INTO meetings(title, date, isOnline,roomID, typeID, statusID, repeatingID) VALUES (@TITLE,@DATE,@ISONLINE,@ROOMID,@TID,@SID,@RID)";
                    }

                }
                else
                {
                    if (request.IsOnline)
                    {
                        sqlInsert = "INSERT INTO meetings(title, date, description, isOnline, link, typeID, statusID, repeatingID) VALUES (@TITLE,@DATE,@DESCR,@ISONLINE,@LINK,@TID,@SID,@RID)";
                    }
                    else
                    {
                        sqlInsert = "INSERT INTO meetings(title, date, description, isOnline, roomID, typeID, statusID, repeatingID) VALUES (@TITLE,@DATE,@DESCR,@ISONLINE,@ROOMID,@TID,@SID,@RID)";
                    }

                }

                using var command = new MySqlCommand(sqlInsert, connection);
                command.Transaction = transaction;
                command.Parameters.AddWithValue("@TITLE", request.Title);
                command.Parameters.AddWithValue("@DATE", request.Date);
                string link = "https://example.com/";
                if (request.Description != null)
                {
                    command.Parameters.AddWithValue("@DESCR", request.Description);
                }
                if (request.IsOnline)
                {
                    command.Parameters.AddWithValue("@ISONLINE", true);
                    link += Guid.NewGuid().ToString();
                    command.Parameters.AddWithValue("@LINK", link);
                }
                else
                {
                    command.Parameters.AddWithValue("@ISONLINE", false);
                    command.Parameters.AddWithValue("@ROOMID", request.RoomID);
                }
                command.Parameters.AddWithValue("@TID", request.TypeOfMeeting);
                command.Parameters.AddWithValue("@SID", 1);
                command.Parameters.AddWithValue("@RID", request.RepetitionOfMeeting);

                if (await command.ExecuteNonQueryAsync() <= 0)
                {
                    throw new Exception();
                }
                if (request.MembersID.Count > 0)
                {
                    var lastID = command.LastInsertedId;

                    string sql = "INSERT INTO meetingsmembers(meetingID,memberID,isCreator) VALUES ";
                    sql += $"({lastID}, {currentUser.ID}, 1), ";
                    for (int i = 0; i < request.MembersID.Count; i++)
                    {
                        if (i != request.MembersID.Count - 1)
                        {
                            sql += $"({lastID}, {request.MembersID.ElementAt(i).ID}, 0), ";
                        }
                        else
                        {
                            sql += $"({lastID}, {request.MembersID.ElementAt(i).ID}, 0)";
                        }
                    }

                    using var membersCommand = new MySqlCommand(sql, connection);
                    membersCommand.Transaction = transaction;

                    if (await membersCommand.ExecuteNonQueryAsync() <= 0)
                    {
                        throw new Exception();
                    }

                }

                await transaction.CommitAsync();

                await connection.CloseAsync();

                return await Task.FromResult(true);

            }
            catch (MySqlException)
            {
                await transaction.RollbackAsync();
                await connection.CloseAsync();
                return await Task.FromResult(false);
            }

        }

        public async Task<bool> DeleteMeetingByID(int id)
        {
            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            await connection.OpenAsync();

            using var command = new MySqlCommand("DELETE FROM meetings WHERE meetings.ID = @ID", connection);
            command.Parameters.AddWithValue("@ID", id);

            if (await command.ExecuteNonQueryAsync() > 0)
            {
                return await Task.FromResult(true);
            }
            else
            {
                return await Task.FromResult(false);
            }
        }

        public async Task<List<MeetingSummaryResponse>?> GetAllMeetingsSummaries()
        {
            List<MeetingSummaryResponse>? response = [];

            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            await connection.OpenAsync();

            using var command = new MySqlCommand("SELECT meetings.ID FROM meetings", connection);

            MySqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                MeetingSummaryResponse? meeting = await GetMeetingSummaryByID(reader.GetInt32(0));
                if (meeting == null)
                {
                    await reader.CloseAsync();
                    await command.DisposeAsync();
                    await connection.CloseAsync();
                    response = null;
                    return await Task.FromResult(response);
                }
                response.Add(meeting);
            }

            return await Task.FromResult(response);
        }

        public async Task<Meeting?> GetMeetingByID(int id)
        {
            Meeting? response = new();
            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            await connection.OpenAsync();

            var transaction = connection.BeginTransaction();

            try
            {
                using var meetingCommand = new MySqlCommand("SELECT meetings.ID, meetings.title, meetings.date, meetings.description, typeofmeeting.type, repetitionofmeeting.repetition, statusofmeeting.status, meetings.isOnline, meetings.link, rooms.roomNumber FROM meetings INNER JOIN typeofmeeting on meetings.typeID = typeofmeeting.ID INNER JOIN repetitionofmeeting on meetings.repeatingID = repetitionofmeeting.ID INNER JOIN statusofmeeting on meetings.statusID = statusofmeeting.ID LEFT JOIN rooms on meetings.roomID = rooms.ID WHERE meetings.ID = @ID", connection);
                meetingCommand.Parameters.AddWithValue("@ID", id);

                meetingCommand.Transaction = transaction;

                MySqlDataReader meetingsReader = await meetingCommand.ExecuteReaderAsync();

                while (await meetingsReader.ReadAsync())
                {
                    response.ID = meetingsReader.GetInt32(0);
                    response.Title = meetingsReader.GetString(1);
                    response.Date = meetingsReader.GetDateTime(2);
                    response.Description = meetingsReader.GetString(3);
                    response.TypeOfMeeting = meetingsReader.GetString(4);
                    response.RepetitionOfMeeting = meetingsReader.GetString(5);
                    response.StatusOfMeeting = meetingsReader.GetString(6);
                    response.IsOnline = meetingsReader.GetBoolean(7);

                    response.Link = meetingsReader.IsDBNull(8) ? string.Empty : meetingsReader.GetString(8);

                    response.RoomNumber = meetingsReader.IsDBNull(9) ? default : meetingsReader.GetInt32(9);

                }

                await meetingsReader.CloseAsync();

                List<User> members = [];

                using var membersCommand = new MySqlCommand("SELECT meetingsmembers.memberID FROM meetingsmembers WHERE meetingsmembers.meetingID = @ID ORDER BY meetingsmembers.isCreator DESC", connection);
                membersCommand.Parameters.AddWithValue("@ID", id);

                membersCommand.Transaction = transaction;

                MySqlDataReader membersReader = await membersCommand.ExecuteReaderAsync() ?? throw new Exception();
                bool creator = true;

                while (await membersReader.ReadAsync())
                {
                    if (creator)
                    {
                        response.Creator = await usersService.GetUserById(membersReader.GetInt32(0));
                        response.IsCreator = membersReader.GetInt32(0) == currentUser.ID;
                        creator = false;
                    }
                    else
                    {
                        User member = await usersService.GetUserById(membersReader.GetInt32(0));
                        members.Add(member);
                    }
                }

                await meetingsReader.CloseAsync();

                List<MeetingMessage> messages = [];

                using var messagesCommand = new MySqlCommand("SELECT meetingschats.ID FROM meetingschats WHERE meetingschats.meetingID = @ID", connection);
                messagesCommand.Parameters.AddWithValue("@ID", id);

                messagesCommand.Transaction = transaction;

                MySqlDataReader messagesReader = await messagesCommand.ExecuteReaderAsync();

                while (await messagesReader.ReadAsync())
                {
                    MeetingMessage message = await messagesService.GetMessageByID(messagesReader.GetInt32(0));
                    messages.Add(message);
                }

                await meetingsReader.CloseAsync();

                List<MeetingAttachment> attachments = [];

                using var attachmentsCommand = new MySqlCommand("SELECT meetingsattachments.ID FROM meetingsattachments WHERE meetingsattachments.meetingID = @ID", connection);

                attachmentsCommand.Parameters.AddWithValue("@ID", id);
                attachmentsCommand.Transaction = transaction;

                MySqlDataReader attachmentsReader = await attachmentsCommand.ExecuteReaderAsync();

                while (await attachmentsReader.ReadAsync())
                {
                    MeetingAttachment attachment = await attachmentsService.GetAttachmentByID(attachmentsReader.GetInt32(0));
                    attachments.Add(attachment);
                }

                response.Members = members;
                response.Messages = messages;
                response.Attachments = attachments;

                await attachmentsReader.CloseAsync();

                await transaction.CommitAsync();

                await connection.CloseAsync();

                return await Task.FromResult(response);
            }
            catch (MySqlException)
            {
                await transaction.RollbackAsync();

                await connection.CloseAsync();

                response = null;

                return await Task.FromResult(response);
            }

        }

        public async Task<List<MeetingSummaryResponse>?> GetMeetingsSummariesFromMyDepartment()
        {
            List<MeetingSummaryResponse>? response = [];

            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            await connection.OpenAsync();

            using var command = new MySqlCommand("SELECT DISTINCT meetings.ID FROM meetings INNER JOIN meetingsmembers ON meetings.ID = meetingsmembers.meetingID INNER JOIN teamsmembers ON meetingsmembers.memberID = teamsmembers.memberID INNER JOIN teamsdepartments ON teamsmembers.teamID = teamsdepartments.teamID INNER JOIN departments ON teamsdepartments.departmentID = departments.ID WHERE departments.directorID = @ID ORDER BY meetings.date", connection);
            command.Parameters.AddWithValue("@ID", currentUser.ID);

            MySqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                MeetingSummaryResponse? meeting = await GetMeetingSummaryByID(reader.GetInt32(0));
                if (meeting == null)
                {
                    await reader.CloseAsync();
                    await command.DisposeAsync();
                    await connection.CloseAsync();
                    response = null;
                    return await Task.FromResult(response);
                }
                response.Add(meeting);
            }

            return await Task.FromResult(response);
        }

        public async Task<List<MeetingSummaryResponse>?> GetMeetingsSummariesFromMyTeam()
        {
            List<MeetingSummaryResponse>? response = [];

            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            await connection.OpenAsync();

            using var command = new MySqlCommand("SELECT DISTINCT meetings.ID FROM meetings INNER JOIN meetingsmembers ON meetings.ID = meetingsmembers.meetingID INNER JOIN teamsmembers ON meetingsmembers.memberID = teamsmembers.memberID WHERE teamsmembers.memberID = @ID AND teamsmembers.isLeader = 1 ORDER BY meetings.ID", connection);
            command.Parameters.AddWithValue("@ID", currentUser.ID);

            MySqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                MeetingSummaryResponse? meeting = await GetMeetingSummaryByID(reader.GetInt32(0));
                if (meeting == null)
                {
                    await reader.CloseAsync();
                    await command.DisposeAsync();
                    await connection.CloseAsync();
                    response = null;
                    return await Task.FromResult(response);
                }
                response.Add(meeting);
            }

            return await Task.FromResult(response);
        }

        public async Task<MeetingSummaryResponse?> GetMeetingSummaryByID(int ID)
        {
            MeetingSummaryResponse? response = new();

            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            await connection.OpenAsync();

            var transaction = connection.BeginTransaction();

            try
            {
                using var command = new MySqlCommand("SELECT meetings.ID, meetings.title, meetings.date, statusofmeeting.status, meetings.isOnline, meetings.link, rooms.roomNumber FROM meetings INNER JOIN statusofmeeting on meetings.statusID = statusofmeeting.ID LEFT JOIN rooms ON meetings.roomID = rooms.ID WHERE meetings.ID=@ID", connection);
                command.Parameters.AddWithValue("@ID", ID);
                command.Transaction = transaction;

                MySqlDataReader reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    response = new()
                    {
                        MeetingID = reader.GetInt32(0),
                        Title = reader.GetString(1),
                        Date = reader.GetDateTime(2),
                        Status = reader.GetString(3),
                        IsOnline = reader.GetBoolean(4),
                        Link = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                        RoomNumber = reader.IsDBNull(6) ? default : reader.GetInt32(6)
                    };
                }

                await reader.CloseAsync();

                using var isLeaderCommand = new MySqlCommand("SELECT meetingsmembers.memberID FROM meetingsmembers WHERE meetingsmembers.meetingID = @ID AND meetingsmembers.memberID = @MID AND meetingsmembers.isCreator = 1", connection);
                isLeaderCommand.Parameters.AddWithValue("@ID", ID);
                isLeaderCommand.Parameters.AddWithValue("@MID", currentUser.ID);

                isLeaderCommand.Transaction = transaction;

                int rows = await isLeaderCommand.ExecuteNonQueryAsync();

                if (rows > 0)
                {
                    response.Creator = true;
                }
                else
                {
                    response.Creator = false;
                }

                await transaction.CommitAsync();
                await connection.CloseAsync();

                return await Task.FromResult(response);
            }
            catch (MySqlException)
            {
                await transaction.RollbackAsync();
                await connection.CloseAsync();
                response = null;
                return await Task.FromResult(response);
            }
        }

        public async Task<List<MeetingSummaryResponse>?> GetMyMeetingSummaries()
        {
            List<MeetingSummaryResponse>? response = [];

            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            await connection.OpenAsync();

            using var command = new MySqlCommand("SELECT meetingsmembers.meetingID FROM meetingsmembers INNER JOIN meetings ON meetingsmembers.meetingID = meetings.ID WHERE meetingsmembers.memberID = @ID ORDER BY meetings.date", connection);
            command.Parameters.AddWithValue("@ID", currentUser.ID);

            MySqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                MeetingSummaryResponse? meeting = await GetMeetingSummaryByID(reader.GetInt32(0));
                if (meeting == null)
                {
                    await reader.CloseAsync();
                    await command.DisposeAsync();
                    await connection.CloseAsync();
                    response = null;
                    return await Task.FromResult(response);
                }
                response.Add(meeting);
            }

            return await Task.FromResult(response);
        }

        public async Task<List<TypeStatusRepetitionOfMeetingResponse>> GetRepetitionOfMeeting()
        {
            List<TypeStatusRepetitionOfMeetingResponse> response = [];

            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            await connection.OpenAsync();

            using var command = new MySqlCommand("SELECT repetitionofmeeting.ID, repetitionofmeeting.repetition FROM repetitionofmeeting", connection);

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

        public async Task<List<RoomResponse>> GetRooms()
        {
            List<RoomResponse> response = [];

            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            await connection.OpenAsync();

            using var command = new MySqlCommand("SELECT rooms.ID, rooms.roomNumber FROM rooms", connection);

            MySqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                response.Add(new()
                {
                    ID = reader.GetInt32(0),
                    Number = reader.GetInt32(1)
                });
            }

            return await Task.FromResult(response);
        }

        public async Task<List<TypeStatusRepetitionOfMeetingResponse>> GetStatusesOfMeeting()
        {
            List<TypeStatusRepetitionOfMeetingResponse> response = [];

            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            await connection.OpenAsync();

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

        public async Task<List<TypeStatusRepetitionOfMeetingResponse>> GetTypesOfMeeting()
        {
            List<TypeStatusRepetitionOfMeetingResponse> response = [];

            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            await connection.OpenAsync();

            using var command = new MySqlCommand("SELECT typeofmeeting.ID, typeofmeeting.type FROM typeofmeeting", connection);

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

        public async Task<bool> UpdateMeeting(CreateMeetingRequest request)
        {

            using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

            await connection.OpenAsync();

            var transaction = await connection.BeginTransactionAsync();

            try
            {
                using var checkCommand = new MySqlCommand("SELECT meetings.isOnline FROM meetings WHERE ID=@ID", connection);

                checkCommand.Parameters.AddWithValue("@ID", request.ID);
                checkCommand.Transaction = transaction;

                MySqlDataReader checkReader = await checkCommand.ExecuteReaderAsync();

                bool wasOnline = default;

                if (!checkReader.HasRows)
                {
                    throw new Exception();
                }

                while (await checkReader.ReadAsync())
                {
                    wasOnline = checkReader.GetBoolean(0);
                }

                await checkReader.CloseAsync();

                var sqlQuery = string.Empty;

                string link = "https://example.com/";

                if (wasOnline && !request.IsOnline)
                {
                    sqlQuery = "UPDATE meetings SET title=@T, date=@D, description=@DESCR, isOnline='0', link=NULL, roomID=@ROOMID, typeID=@TID, statusID=@SID, repeatingID=@RID WHERE ID=@ID";
                }
                else if (!wasOnline && request.IsOnline)
                {
                    link += Guid.NewGuid().ToString();
                    sqlQuery = "UPDATE meetings SET title=@T, date=@D, description=@DESCR, isOnline='1', link=@LINK, roomID=NULL, typeID=@TID, statusID=@SID, repeatingID=@RID WHERE ID=@ID";
                }
                else if (!wasOnline && !request.IsOnline)
                {
                    sqlQuery = "UPDATE meetings SET title=@T, date=@D, description=@DESCR, roomID=@ROOMID, typeID=@TID, statusID=@SID, repeatingID=@RID WHERE ID=@ID";
                }
                else if (wasOnline && request.IsOnline)
                {
                    sqlQuery = "UPDATE meetings SET title=@T, date=@D, description=@DESCR, typeID=@TID, statusID=@SID, repeatingID=@RID WHERE ID=@ID";
                }

                using var command = new MySqlCommand(sqlQuery, connection);

                command.Parameters.AddWithValue("@T", request.Title);
                command.Parameters.AddWithValue("@D", request.Date);
                command.Parameters.AddWithValue("@DESCR", request.Description ?? "Empty description");
                command.Parameters.AddWithValue("@TID", request.TypeOfMeeting);
                command.Parameters.AddWithValue("@SID", request.StatusOfMeeting);
                command.Parameters.AddWithValue("@RID", request.RepetitionOfMeeting);
                command.Parameters.AddWithValue("@ID", request.ID);

                command.Transaction = transaction;

                if (wasOnline && !request.IsOnline)
                {
                    command.Parameters.AddWithValue("@ROOMID", request.RoomID);
                }
                else if (!wasOnline && request.IsOnline)
                {
                    command.Parameters.AddWithValue("@LINK", link);
                }
                else if (!wasOnline && !request.IsOnline)
                {
                    command.Parameters.AddWithValue("@ROOMID", request.RoomID);
                }

                await command.ExecuteNonQueryAsync();

                if (request.MembersID.Count <= 0)
                {
                    await transaction.CommitAsync();
                    await connection.CloseAsync();
                    return await Task.FromResult(true);

                }
                var listToAdd = request.MembersID.Where(u => u.IsSelected).ToList();
                if (listToAdd.Count > 0)
                {
                    var sql = "INSERT INTO meetingsmembers(meetingID, memberID) VALUES ";
                    for (int i = 0; i < listToAdd.Count; i++)
                    {
                        if (i == listToAdd.Count - 1)
                        {
                            sql += $"({request.ID}, {listToAdd.ElementAt(i).ID})";
                        }
                        else
                        {
                            sql += $"({request.ID}, {listToAdd.ElementAt(i).ID}), ";
                        }
                    }

                    using var addMembersCommand = new MySqlCommand(sql, connection);

                    addMembersCommand.Transaction = transaction;

                    await addMembersCommand.ExecuteNonQueryAsync();

                }
                var listToDelete = request.MembersID.Where(u => !u.IsSelected).ToList();
                if (listToDelete.Count > 0)
                {
                    var sql = $"DELETE FROM meetingsmembers WHERE meetingID = {request.ID} AND memberID IN (";

                    for (int i = 0; i < listToDelete.Count; i++)
                    {
                        if (i == listToDelete.Count - 1)
                        {
                            sql += $"{listToDelete.ElementAt(i).ID})";
                        }
                        else
                        {
                            sql += $"{listToDelete.ElementAt(i).ID}, ";
                        }
                    }

                    using var deleteMembersCommand = new MySqlCommand(sql, connection);

                    deleteMembersCommand.Transaction = transaction;

                    await deleteMembersCommand.ExecuteNonQueryAsync();

                }

                await transaction.CommitAsync();

                await connection.CloseAsync();

                return await Task.FromResult(true);
            }
            catch (MySqlException)
            {
                await transaction.RollbackAsync();

                await connection.CloseAsync();

                return await Task.FromResult(false);
            }
        }
    }
}

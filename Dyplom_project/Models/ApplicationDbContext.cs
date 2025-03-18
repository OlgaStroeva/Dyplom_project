
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Neo4j.Driver;

public class ApplicationDbContext : IDisposable
{
    private IDriver? _driver;

    // Конструктор без параметров (для тестов)
    public ApplicationDbContext() { }

    public ApplicationDbContext(IConfiguration configuration)
    {
        string uri = configuration["Memgraph:Uri"] ?? "bolt://localhost:7687";
        string user = configuration["Memgraph:User"] ?? "neo4j";
        string password = configuration["Memgraph:Password"] ?? "password";

        _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
    }

    public virtual async Task CreateUserAsync(User user)
    {
        await using var session = _driver!.AsyncSession();
        await session.WriteTransactionAsync(async tx =>
        {
            await tx.RunAsync(
                "CREATE (u:User {id: $id, name: $name, email: $email, password: $password})",
                new { id = user.Id, name = user.Name, email = user.Email, password = user.PasswordHash }
            );
        });
    }

    public virtual async Task<User?> GetUserByEmailAsync(string email)
    {
        await using var session = _driver!.AsyncSession();
        return await session.ReadTransactionAsync(async tx =>
        {
            var result = await tx.RunAsync(
                @"MATCH (u:User {email: $email}) RETURN u.id AS id, u.name AS name, u.email AS email, u.password AS password",
                new { email }
            );

            var records = await result.ToListAsync();
            var record = records.FirstOrDefault();
            return record == null ? null : new User
            {
                Id = record["id"].As<int>(),
                Name = record["name"].As<string>(),
                Email = record["email"].As<string>(),
                PasswordHash = record["password"].As<string>()
            };
        });
    }
    
    public void Dispose()
    {
        _driver?.Dispose();
    }
    
    public virtual async Task<Event?> GetEventByIdAsync(int eventId)
    {
        await using var session = _driver.AsyncSession();
        return await session.ReadTransactionAsync(async tx =>
        {
            var result = await tx.RunAsync(
                @"MATCH (e:Event {id: $eventId}) 
              RETURN e.id AS id, e.name AS name, e.description AS description, 
                     e.imageUrl AS imageUrl, e.invitationTemplateId AS invitationTemplateId, 
                     e.createdBy AS createdBy",
                new { eventId }
            );

            var records = await result.ToListAsync();
            if (!records.Any()) return null; // Если мероприятие не найдено, возвращаем null

            var record = records.First();
            return new Event
            {
                Id = record["id"].As<int>(),
                Name = record["name"].As<string>(),
                Description = record["description"].As<string>(),
                ImageUrl = record["imageUrl"].As<string>(),
                InvitationTemplateId = record["invitationTemplateId"].As<int>(),
                CreatedBy = record["createdBy"].As<int>()
            };
        });
    }
    
    public virtual async Task<List<Event>> GetUserEventsAsync(int userId)
    {
        await using var session = _driver.AsyncSession();
        return await session.ReadTransactionAsync(async tx =>
        {
            var result = await tx.RunAsync(
                @"MATCH (u:User {id: $userId})-[:CREATED_BY]->(e:Event) 
              RETURN e.id AS id, e.name AS name, e.description AS description, 
                     e.imageUrl AS imageUrl, e.invitationTemplateId AS invitationTemplateId, 
                     e.createdBy AS createdBy",
                new { userId }
            );

            var events = await result.ToListAsync();
            return events.Select(record => new Event
            {
                Id = record["id"].As<int>(),
                Name = record["name"].As<string>(),
                Description = record["description"].As<string>(),
                ImageUrl = record["imageUrl"].As<string>(),
                InvitationTemplateId = record["invitationTemplateId"].As<int>(),
                CreatedBy = record["createdBy"].As<int>()
            }).ToList();
        });
    }

    public virtual async Task CreateEventAsync(Event newEvent)
    {
        await using var session = _driver.AsyncSession();
        await session.WriteTransactionAsync(async tx =>
        {
            await tx.RunAsync(
                @"
            CREATE (e:Event {id: $id, name: $name, description: $description, imageUrl: $imageUrl, invitationTemplateId: $invitationTemplateId, createdBy: $createdBy})
            WITH e
            MATCH (u:User {id: $createdBy})
            CREATE (u)-[:CREATED_BY]->(e)
            ",
                new
                {
                    id = newEvent.Id,
                    name = newEvent.Name,
                    description = newEvent.Description,
                    imageUrl = newEvent.ImageUrl,
                    invitationTemplateId = newEvent.InvitationTemplateId,
                    createdBy = newEvent.CreatedBy
                }
            );
        });
    }
    public virtual async Task UpdateEventAsync(Event updatedEvent)
    {
        await using var session = _driver.AsyncSession();
        await session.WriteTransactionAsync(async tx =>
        {
            await tx.RunAsync(
                @"
            MATCH (e:Event {id: $id})
            SET e.description = $description,
                e.imageUrl = $imageUrl,
                e.invitationTemplateId = $invitationTemplateId
            ",
                new
                {
                    id = updatedEvent.Id,
                    description = updatedEvent.Description,
                    imageUrl = updatedEvent.ImageUrl,
                    invitationTemplateId = updatedEvent.InvitationTemplateId
                }
            );
        });
    }
    public virtual async Task<bool> EventHasFormAsync(int eventId)
    {
        await using var session = _driver.AsyncSession();
        var result = await session.RunAsync(
            @"MATCH (e:Event {id: $eventId})-[:HAS_FORM]->(:Form) RETURN COUNT(*) AS count",
            new { eventId }
        );
        var record = await result.SingleAsync();
        return record["count"].As<int>() > 0;
    }

    public virtual async Task CreateFormAsync(int eventId)
    {
        if (await EventHasFormAsync(eventId))
            throw new InvalidOperationException("У мероприятия уже есть анкета.");

        await using var session = _driver.AsyncSession();
        await session.WriteTransactionAsync(async tx =>
        {
            await tx.RunAsync(
                @"MATCH (e:Event {id: $eventId})
              CREATE (f:Form {id: $formId, eventId: $eventId, fields: ['Email']})
              CREATE (e)-[:HAS_FORM]->(f)",
                new { eventId, formId = new Random().Next(10000, 99999) }
            );
        });
    }

    public virtual async Task UpdateFormAsync(int formId, List<string> fields)
    {
        if (await FormHasParticipantsAsync(formId))
            throw new InvalidOperationException("Нельзя редактировать анкету, так как она содержит данные участников.");

        await using var session = _driver.AsyncSession();
        await session.WriteTransactionAsync(async tx =>
        {
            await tx.RunAsync(
                @"MATCH (f:Form {id: $formId}) SET f.fields = $fields",
                new { formId, fields }
            );
        });
    }

    public virtual async Task<bool> FormHasParticipantsAsync(int formId)
    {
        await using var session = _driver.AsyncSession();
        var result = await session.RunAsync(
            @"MATCH (:Form {id: $formId})-[:HAS_PARTICIPANT_DATA]->(:ParticipantData) RETURN COUNT(*) AS count",
            new { formId }
        );
        var record = await result.SingleAsync();
        return record["count"].As<int>() > 0;
    }

    public virtual async Task DeleteFormAsync(int formId)
    {
        if (await FormHasParticipantsAsync(formId))
            throw new InvalidOperationException("Нельзя удалить анкету, так как она содержит данные участников.");

        await using var session = _driver.AsyncSession();
        await session.WriteTransactionAsync(async tx =>
        {
            await tx.RunAsync(
                @"MATCH (f:Form {id: $formId}) DETACH DELETE f",
                new { formId }
            );
        });
    }

    public virtual async Task<List<User>> FindUsersByEmailAsync(string emailPart)
    {
        await using var session = _driver.AsyncSession();
        return await session.ReadTransactionAsync(async tx =>
        {
            var result = await tx.RunAsync(
                @"MATCH (u:User) 
              WHERE u.email CONTAINS $emailPart 
              RETURN u.id AS id, u.name AS name, u.email AS email",
                new { emailPart }
            );

            var users = await result.ToListAsync();
            return users.Select(record => new User
            {
                Id = record["id"].As<int>(),
                Name = record["name"].As<string>(),
                Email = record["email"].As<string>()
            }).ToList();
        });
    }

    public virtual async Task AddStaffAsync(int eventId, int userId)
    {
        await using var session = _driver.AsyncSession();
        await session.WriteTransactionAsync(async tx =>
        {
            await tx.RunAsync(
                @"MATCH (e:Event {id: $eventId}), (u:User {id: $userId})
              CREATE (u)-[:STAFF]->(e)",
                new { eventId, userId }
            );
        });
    }
    public virtual async Task RemoveStaffAsync(int eventId, int userId)
    {
        await using var session = _driver.AsyncSession();
        await session.WriteTransactionAsync(async tx =>
        {
            await tx.RunAsync(
                @"MATCH (u:User {id: $userId})-[s:STAFF]->(e:Event {id: $eventId}) DELETE s",
                new { eventId, userId }
            );
        });
    }

    public virtual async Task LeaveEventAsync(int eventId, int userId)
    {
        await using var session = _driver.AsyncSession();
        await session.WriteTransactionAsync(async tx =>
        {
            await tx.RunAsync(
                @"MATCH (u:User {id: $userId})-[s:STAFF]->(e:Event {id: $eventId}) DELETE s",
                new { eventId, userId }
            );
        });
    }

    public virtual async Task<Event?> GetEventByFormIdAsync(int formId)
    {
        await using var session = _driver.AsyncSession();
        return await session.ReadTransactionAsync(async tx =>
        {
            var result = await tx.RunAsync(
                @"MATCH (e:Event)-[:HAS_FORM]->(f:Form {id: $formId}) 
              RETURN e.id AS id, e.createdBy AS createdBy",
                new { formId }
            );

            var record = await result.SingleAsync();
            return record == null ? null : new Event
            {
                Id = record["id"].As<int>(),
                CreatedBy = record["createdBy"].As<int>()
            };
        });
    }

    public virtual async Task<byte[]> GenerateFormTemplateXlsx(int formId)
    {
        await using var session = _driver.AsyncSession();
        var result = await session.RunAsync(
            @"MATCH (f:Form {id: $formId}) RETURN f.fields AS fields",
            new { formId }
        );

        var records = await result.ToListAsync();
        var record = records.FirstOrDefault(); 
        if (record == null) throw new InvalidOperationException("Анкета не найдена.");

        var fields = record["fields"].As<List<string>>();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Анкета");

        // Заполняем заголовки столбцов
        for (int i = 0; i < fields.Count; i++)
        {
            worksheet.Cell(1, i + 1).Value = fields[i];
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private bool IsValidEmail(string email)
    {
        return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    }

    public virtual async Task<List<string>> AddParticipantDataAsync(int formId, List<Dictionary<string, string>> participants)
    {
        var errors = new List<string>();
        var validParticipants = new List<Dictionary<string, string>>();

        for (int i = 0; i < participants.Count; i++)
        {
            var participant = participants[i];

            if (!participant.ContainsKey("Email") || !IsValidEmail(participant["Email"]))
            {
                errors.Add($"Строка {i + 1}: Некорректный email");
                continue;
            }

            if (participant.Values.Any(value => string.IsNullOrWhiteSpace(value)))
            {
                errors.Add($"Строка {i + 1}: Не все поля заполнены");
                continue;
            }

            validParticipants.Add(participant);
        }

        if (validParticipants.Count > 0)
        {
            await using var session = _driver.AsyncSession();
            await session.WriteTransactionAsync(async tx =>
            {
                foreach (var participant in validParticipants)
                {
                    await tx.RunAsync(
                        @"MATCH (f:Form {id: $formId})
                      CREATE (p:ParticipantData {id: $participantId, formId: $formId, data: $data})
                      CREATE (f)-[:HAS_PARTICIPANT_DATA]->(p)",
                        new { formId, participantId = new Random().Next(10000, 99999), data = participant }
                    );
                }
            });
        }

        return errors;
    }

    public virtual async Task<List<string>> ParseXlsxParticipants(int formId, IFormFile file)
    {
        var errors = new List<string>();
        var participants = new List<Dictionary<string, string>>();

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1);
        var headers = worksheet.Row(1).CellsUsed().Select(cell => cell.Value.ToString()).ToList();

        for (int row = 2; row <= worksheet.LastRowUsed().RowNumber(); row++)
        {
            var participant = new Dictionary<string, string>();

            for (int col = 1; col <= headers.Count; col++)
            {
                participant[headers[col - 1]] = worksheet.Cell(row, col).GetString();
            }

            participants.Add(participant);
        }

        errors.AddRange(await AddParticipantDataAsync(formId, participants));
        return errors;
    }

    public virtual async Task<bool> AddQrCodeToParticipantAsync(int participantId, string qrCodeBase64)
    {
        await using var session = _driver.AsyncSession();
        var result = await session.RunAsync(
            @"MATCH (p:ParticipantData {id: $participantId}) RETURN p",
            new { participantId }
        );

        if (!await result.FetchAsync()) return false;

        await session.WriteTransactionAsync(async tx =>
        {
            await tx.RunAsync(
                @"MATCH (p:ParticipantData {id: $participantId}) 
              SET p.qrCode = $qrCodeBase64",
                new { participantId, qrCodeBase64 }
            );
        });

        return true;
    }

}


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

    public async Task<User?> GetUserByResetTokenAsync(string token)
    {
        await using var session = _driver.AsyncSession();
        var result = await session.RunAsync(
            @"MATCH (u:User {passwordResetToken: $token})
          RETURN u.id AS id, u.name AS name, u.email AS email, u.passwordHash AS passwordHash,
                 u.canBeStaff AS canBeStaff, u.isEmailConfirmed AS isEmailConfirmed,
                 u.emailConfirmationCode AS emailConfirmationCode, u.passwordResetToken AS passwordResetToken",
            new { token }
        );

        var record = (await result.ToListAsync()).FirstOrDefault();
        return record == null ? null : new User
        {
            Id = record["id"].As<int>(),
            Name = record["name"].As<string>(),
            Email = record["email"].As<string>(),
            PasswordHash = record["passwordHash"].As<string>(),
            CanBeStaff = record["canBeStaff"].As<bool>(),
            IsEmailConfirmed = record["isEmailConfirmed"].As<bool>(),
            EmailConfirmationCode = record["emailConfirmationCode"].As<string>(),
            PasswordResetToken = record["passwordResetToken"].As<string>()
        };
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
    
    public async Task<Event?> GetEventByIdAsync(int eventId)
    {
        await using var session = _driver.AsyncSession();
        var result = await session.RunAsync(
            @"MATCH (e:Event {id: $eventId})
          RETURN e.id AS id, e.name AS name, e.description AS description,
                 e.imageBase64 AS imageBase64, e.createdBy AS createdBy,
                 e.dateTime AS dateTime, e.category AS category, e.location AS location",
            new { eventId });

        var record = (await result.ToListAsync()).FirstOrDefault();
        if (record == null) return null;

        return new Event
        {
            Id = record["id"].As<int>(),
            Name = record["name"].As<string>(),
            Description = record["description"].As<string>(),
            ImageBase64 = record["imageBase64"].As<string>(),
            CreatedBy = record["createdBy"].As<int>(),
            DateTime = record["dateTime"].As<string>(),
            Category = record["category"].As<string>(),
            Location = record["location"].As<string>()
        };
    }
    
    public async Task<List<Event>> GetUserEventsAsync(int userId)
    {
        await using var session = _driver.AsyncSession();
        var result = await session.RunAsync(
            @"MATCH (u:User {id: $userId})-[:CREATED]->(e:Event)
          RETURN e.id AS id,
                 e.name AS name,
                 e.description AS description,
                 e.imageBase64 AS imageBase64,
                 e.dateTime AS dateTime,
                 e.category AS category,
                 e.location AS location,
                 e.createdBy AS createdBy",
            new { userId }
        );

        var records = await result.ToListAsync();

        return records.Select(r => new Event
        {
            Id = r["id"].As<int>(),
            Name = r["name"].As<string>(),
            Description = r["description"].As<string>(),
            ImageBase64 = r["imageBase64"].As<string>(),
            DateTime = r["dateTime"].As<string>(),
            Category = r["category"].As<string>(),
            Location = r["location"].As<string>(),
            CreatedBy = r["createdBy"].As<int>()
        }).ToList();
    }


    public async Task<int> CreateEventAsync(Event evt)
    {
        evt.Id = new Random().Next(10000, 99999);

        await using var session = _driver.AsyncSession();
        await session.WriteTransactionAsync(async tx =>
        {
            await tx.RunAsync(
                @"MATCH (u:User {id: $userId})
              CREATE (e:Event {
                id: $id,
                name: $name,
                description: $description,
                imageBase64: $imageBase64,
                dateTime: $dateTime,
                category: $category,
                location: $location,
                createdBy: $userId
              })
              CREATE (u)-[:CREATED]->(e)",
                new
                {
                    id = evt.Id,
                    name = evt.Name,
                    description = evt.Description,
                    imageBase64 = evt.ImageBase64,
                    dateTime = evt.DateTime,
                    category = evt.Category,
                    location = evt.Location,
                    userId = evt.CreatedBy
                });
        });

        return evt.Id;
    }

    public async Task UpdateEventAsync(int eventId, UpdateEventRequest request)
    {
        await using var session = _driver.AsyncSession();
        await session.WriteTransactionAsync(async tx =>
        {
            await tx.RunAsync(
                @"MATCH (e:Event {id: $eventId})
              SET e.name = $name,
                  e.description = $description,
                  e.imageBase64 = $imageBase64,
                  e.dateTime = $dateTime,
                  e.category = $category,
                  e.location = $location",
                new
                {
                    eventId,
                    name = request.Name ?? "",
                    description = request.Description ?? "",
                    imageBase64 = request.ImageBase64 ?? "",
                    dateTime = request.DateTime ?? "",
                    category = request.Category ?? "",
                    location = request.Location ?? ""
                });
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

    public async Task<int> CreateFormAsync(int eventId)
    {
        if (await EventHasFormAsync(eventId))
            throw new InvalidOperationException("У мероприятия уже есть анкета.");

        int invitationTemplateId = new Random().Next(10000, 99999);

        await using var session = _driver.AsyncSession();
        await session.WriteTransactionAsync(async tx =>
        {
            await tx.RunAsync(
                @"
            MATCH (e:Event {id: $eventId})
            CREATE (f:Form {id: $formId, eventId: $eventId, fields: ['Email']})
            CREATE (e)-[:HAS_FORM]->(f)
            SET e.invitationTemplateId = $formId
            ",
                new { formId = invitationTemplateId, eventId = eventId }
            );
        });

        return invitationTemplateId;
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

    public virtual async Task DeleteFormAsync(int formId, int eventId)
    {
        if (await FormHasParticipantsAsync(formId))
            throw new InvalidOperationException("Нельзя удалить анкету, так как она содержит данные участников.");

        await using var session = _driver.AsyncSession();
        Console.WriteLine(eventId);
        await session.WriteTransactionAsync(async tx =>
        {
            await tx.RunAsync(
                @"MATCH (e:Event {id: $eventId})
                        MATCH (f:Form {id: $formId}) 
                        SET e.invitationTemplateId = $zero
                        DETACH DELETE f",
                new { formId, eventId, zero = 0 }
            );
        });
    }
    
    public async Task<List<User>> FindUsersByEmailAsync(string emailPart)
    {
        await using var session = _driver.AsyncSession();
        var result = await session.RunAsync(
            @"MATCH (u:User)
          WHERE u.email CONTAINS $emailPart AND u.canBeStaff = true
          RETURN u.id AS id,
                 u.name AS name,
                 u.email AS email",
            new { emailPart }
        );

        var records = await result.ToListAsync();

        return records.Select(r => new User
        {
            Id = r["id"].As<int>(),
            Name = r["name"].As<string>(),
            Email = r["email"].As<string>()
        }).ToList();
    }

    public async Task<bool> AddStaffAsync(int eventId, int userId)
    {
        await using var session = _driver.AsyncSession();

        return await session.WriteTransactionAsync(async tx =>
        {
            // Проверяем, что пользователь существует и может быть назначен сотрудником
            var check = await tx.RunAsync(
                @"MATCH (u:User {id: $userId})
              RETURN u.canBeStaff AS canBeStaff",
                new { userId }
            );

            var record = (await check.ToListAsync()).FirstOrDefault();

            if (record == null || !record["canBeStaff"].As<bool>())
            {
                return false; // Пользователь не найден или запретил быть сотрудником
            }

            await tx.RunAsync(
                @"MATCH (e:Event {id: $eventId}), (u:User {id: $userId})
              MERGE (u)-[:STAFF_OF]->(e)",
                new { eventId, userId });

            return true;
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

    public async Task<Event?> GetEventByFormIdAsync(int formId)
    {
        await using var session = _driver.AsyncSession();
        return await session.ReadTransactionAsync(async tx =>
        {
            var result = await tx.RunAsync(
                @"MATCH (e:Event)-[:HAS_FORM]->(f:Form {id: $formId}) 
              RETURN e.id AS id, e.name AS name, e.createdBy AS createdBy",
                new { formId }
            );

            var records = await result.ToListAsync();
            var record = records.FirstOrDefault();

            return record == null ? null : new Event
            {
                Id = record["id"].As<int>(),
                Name = record["name"].As<string>(),
                CreatedBy = record["createdBy"].As<int>()
            };
        });
    }

    public async Task AssignStaffToEventAsync(int eventId, int userId)
    {
        await using var session = _driver.AsyncSession();
        await session.WriteTransactionAsync(async tx =>
        {
            await tx.RunAsync(
                @"MATCH (u:User {id: $userId}), (e:Event {id: $eventId})
              MERGE (u)-[:STAFF_FOR]->(e)", // MERGE = только если связи ещё нет
                new { userId, eventId });
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

    public async Task<User?> GetUserByConfirmationCodeAsync(string code)
    {
        await using var session = _driver.AsyncSession();
        var result = await session.RunAsync(
            @"MATCH (u:User {emailConfirmationCode: $code}) 
          RETURN u.id AS id, u.name AS name, u.email AS email, 
                 u.passwordHash AS passwordHash, u.canBeStaff AS canBeStaff,
                 u.isEmailConfirmed AS isEmailConfirmed, u.emailConfirmationCode AS emailConfirmationCode",
            new { code }
        );

        var record = (await result.ToListAsync()).FirstOrDefault();
        return record == null ? null : new User
        {
            Id = record["id"].As<int>(),
            Name = record["name"].As<string>(),
            Email = record["email"].As<string>(),
            PasswordHash = record["passwordHash"].As<string>(),
            CanBeStaff = record["canBeStaff"].As<bool>(),
            IsEmailConfirmed = record["isEmailConfirmed"].As<bool>(),
            EmailConfirmationCode = record["emailConfirmationCode"].As<string>()
        };
    }

    public async Task<Form?> GetFormByEventIdAsync(int eventId)
    {
        await using var session = _driver.AsyncSession();
        var result = await session.RunAsync(
            @"MATCH (e:Event {id: $eventId})-[:HAS_FORM]->(f:Form)
          RETURN f.id AS id, f.fields AS fields",
            new { eventId }
        );

        var record = (await result.ToListAsync()).FirstOrDefault();
        if (record == null) return null;

        return new Form
        {
            Id = record["id"].As<int>(),
            Fields = record["fields"].As<List<string>>()
        };
    }

    public async Task<FormTemplate?> GetFormTemplateByIdAsync(int templateId)
    {
        await using var session = _driver.AsyncSession();
        var result = await session.RunAsync(
            @"MATCH (t:FormTemplate {id: $templateId})
          RETURN t.id AS id, t.subject AS subject, t.body AS body",
            new { templateId }
        );

        var record = (await result.ToListAsync()).FirstOrDefault();
        if (record == null) return null;

        return new FormTemplate
        {
            Id = record["id"].As<int>(),
            Subject = record["subject"].As<string>(),
            Body = record["body"].As<string>()
        };
    }

    
    public async Task UpdateUserAsync(User user)
    {
        await using var session = _driver.AsyncSession();
        await session.WriteTransactionAsync(async tx =>
        {
            await tx.RunAsync(
                @"MATCH (u:User {id: $id})
              SET u.passwordHash = $passwordHash,
                  u.passwordResetToken = $passwordResetToken,
                  u.passwordResetRequestedAt = $requestedAt,
                  u.passwordResetAttempts = $attempts",
                new
                {
                    id = user.Id,
                    passwordHash = user.PasswordHash,
                    passwordResetToken = user.PasswordResetToken,
                    requestedAt = user.PasswordResetRequestedAt,
                    attempts = user.PasswordResetAttempts
                });
        });
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        await using var session = _driver.AsyncSession();
        var result = await session.RunAsync(
            @"MATCH (u:User {id: $userId})
          RETURN u.id AS id, u.name AS name, u.email AS email, u.canBeStaff AS canBeStaff",
            new { userId }
        );

        var record = (await result.ToListAsync()).FirstOrDefault();
        if (record == null) return null;

        return new User
        {
            Id = record["id"].As<int>(),
            Name = record["name"].As<string>(),
            Email = record["email"].As<string>(),
            CanBeStaff = record["canBeStaff"].As<bool>()
        };
    }

    
public async Task<List<string>> AddParticipantDataAsync(int formId, List<Dictionary<string, string>> participants)
{
    var errors = new List<string>();
    var validParticipants = new List<Dictionary<string, string>>();

    // Получаем структуру анкеты
    await using var session = _driver.AsyncSession();
    var formResult = await session.RunAsync(
        @"MATCH (f:Form {id: $formId}) RETURN f.fields AS fields",
        new { formId }
    );

    var formRecords = await formResult.ToListAsync();
    var formRecord = formRecords.FirstOrDefault();

    if (formRecord == null)
    {
        errors.Add("Анкета не найдена.");
        return errors;
    }

    var formFields = formRecord["fields"].As<List<string>>();

    foreach (var (participant, index) in participants.Select((value, i) => (value, i)))
    {
        // Проверяем наличие всех полей анкеты в запросе
        var missingFields = formFields.Except(participant.Keys).ToList();
        var extraFields = participant.Keys.Except(formFields).ToList();

        if (missingFields.Any())
        {
            errors.Add($"Строка {index + 1}: Отсутствуют поля: {string.Join(", ", missingFields)}.");
            continue;
        }

        if (extraFields.Any())
        {
            errors.Add($"Строка {index + 1}: Лишние поля: {string.Join(", ", extraFields)}.");
            continue;
        }

        // Проверяем email
        if (!participant.ContainsKey("Email") || !IsValidEmail(participant["Email"]))
        {
            errors.Add($"Строка {index + 1}: Некорректный email.");
            continue;
        }

        validParticipants.Add(participant);
    }

    // Добавляем только корректных участников
    if (validParticipants.Count > 0)
    {
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

    public async Task<List<ParticipantData>> GetAllParticipantDataAsync(int formId)
    {
        await using var session = _driver.AsyncSession();

        var result = await session.RunAsync(
            @"MATCH (f:Form {id: $formId})-[:HAS_PARTICIPANT_DATA]->(p:ParticipantData)
          RETURN p.id AS id, p.data AS data",
            new { formId }
        );

        var records = await result.ToListAsync();

        return records.Select(r => new ParticipantData
        {
            Id = r["id"].As<int>(),
            FormId = formId,
            Data = r["data"].As<Dictionary<string, string>>()
        }).ToList();
    }
    
}

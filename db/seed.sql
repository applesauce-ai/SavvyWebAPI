/*
    seed.sql — mockup / demo data for SavvyDb (idempotent; safe to re-run).

    Mirrors the runtime seeder (Savvy.Infrastructure.Persistence.SavvySeeder).
    Run against a database already created by schema.sql (or EF migrations):

        sqlcmd -S localhost -E -d SavvyDb -i db\seed.sql

    Demo credentials (LOCAL/DEV ONLY — the PasswordHash values below are
    ASP.NET Core Identity PBKDF2 hashes of these passwords):
        admin@savvy.test      Admin#12345        (Admin,           no practice)
        manager@savvy.test    Manager#12345      (PracticeManager, Savvy Medical Practice)
        clinician@savvy.test  Clinician#12345    (Clinician,       Savvy Medical Practice)
*/
SET NOCOUNT ON;

/* ---------- Roles ---------- */
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'Admin')            INSERT INTO Roles (Name) VALUES ('Admin');
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'PracticeManager') INSERT INTO Roles (Name) VALUES ('PracticeManager');
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'Clinician')       INSERT INTO Roles (Name) VALUES ('Clinician');

/* ---------- Practice ---------- */
IF NOT EXISTS (SELECT 1 FROM Practices WHERE Name = 'Savvy Medical Practice')
    INSERT INTO Practices (Name) VALUES ('Savvy Medical Practice');

DECLARE @practiceId int = (SELECT Id FROM Practices WHERE Name = 'Savvy Medical Practice');
DECLARE @adminRole  int = (SELECT Id FROM Roles WHERE Name = 'Admin');
DECLARE @mgrRole    int = (SELECT Id FROM Roles WHERE Name = 'PracticeManager');
DECLARE @clinRole   int = (SELECT Id FROM Roles WHERE Name = 'Clinician');

/* ---------- Users ---------- */
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'admin@savvy.test')
    INSERT INTO Users (PublicId, Email, PasswordHash, RoleId, PracticeId)
    VALUES ('11111111-1111-1111-1111-111111111111', 'admin@savvy.test',
            'AQAAAAIAAYagAAAAEKjfLY8ybaO1ORoTbp6oXErpl0a6m2twAugSk6f12a3L3xMj68zs0Map5d/IkGGtRg==',
            @adminRole, NULL);

IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'manager@savvy.test')
    INSERT INTO Users (PublicId, Email, PasswordHash, RoleId, PracticeId)
    VALUES ('22222222-2222-2222-2222-222222222222', 'manager@savvy.test',
            'AQAAAAIAAYagAAAAEP0gev1ceczu3MEmJPzxDG9K/30KWfPhVnnN6vlsCTXgA7DAtdxz2SHqbub1fyNxuA==',
            @mgrRole, @practiceId);

IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'clinician@savvy.test')
    INSERT INTO Users (PublicId, Email, PasswordHash, RoleId, PracticeId)
    VALUES ('33333333-3333-3333-3333-333333333333', 'clinician@savvy.test',
            'AQAAAAIAAYagAAAAELKNoXYXkiOS/lDBB+e1AhmVGf+ge4CRHv9ha3b8HguQmBP7Ei20EObqTcxntS283Q==',
            @clinRole, @practiceId);

DECLARE @clinId int = (SELECT Id FROM Users WHERE Email = 'clinician@savvy.test');

/* ---------- Shifts (Status: 0 = Open, 1 = Completed) ---------- */
IF NOT EXISTS (SELECT 1 FROM Shifts)
BEGIN
    INSERT INTO Shifts (PracticeId, ClinicianId, [Date], StartUtc, EndUtc, HourlyRate, [Role], Location, Status)
    VALUES
        -- Assigned to the clinician, in the past -> ready to be timesheeted
        (@practiceId, @clinId, '2026-07-14', '2026-07-14T08:00:00', '2026-07-14T16:00:00', 25.00, 'Nurse',  'Main Ward', 0),
        -- Open and unassigned (not yet claimed)
        (@practiceId, NULL,    '2026-07-16', '2026-07-16T08:00:00', '2026-07-16T16:00:00', 25.00, 'Nurse',  'Main Ward', 0),
        -- Assigned to the clinician, in the future
        (@practiceId, @clinId, '2026-07-18', '2026-07-18T09:00:00', '2026-07-18T17:00:00', 30.00, 'Doctor', 'Clinic A',  0);
END

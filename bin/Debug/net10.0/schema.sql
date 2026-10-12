IF OBJECT_ID(N'dbo.Messages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Messages (
        Id             INT IDENTITY(1,1) PRIMARY KEY,
        ConversationId UNIQUEIDENTIFIER NOT NULL,
        Sender         VARCHAR(10)   NOT NULL CHECK (Sender IN ('user', 'bot')),
        [Text]         NVARCHAR(MAX) NOT NULL,
        CreatedAt      DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
ELSE IF COL_LENGTH(N'dbo.Messages', N'ConversationId') IS NULL
BEGIN
    ALTER TABLE dbo.Messages ADD ConversationId UNIQUEIDENTIFIER NULL;
    UPDATE dbo.Messages
        SET ConversationId = '00000000-0000-0000-0000-000000000001'
        WHERE ConversationId IS NULL;
    ALTER TABLE dbo.Messages ALTER COLUMN ConversationId UNIQUEIDENTIFIER NOT NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Messages_ConversationId_Id'
      AND object_id = OBJECT_ID(N'dbo.Messages')
)
    CREATE INDEX IX_Messages_ConversationId_Id ON dbo.Messages (ConversationId, Id);

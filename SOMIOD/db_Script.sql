-- 1. Criar a Base de Dados
-- Isto irá gerar os ficheiros .mdf e .ldf na pasta predefinida do seu SQL Server
CREATE DATABASE SOMIOD_DB;
GO

USE SOMIOD_DB;
GO

-- 2. Tabela: Application
-- Representa a aplicação do mundo real [cite: 44]
CREATE TABLE Application (
    Id INT IDENTITY(1,1) PRIMARY KEY, -- Identificador interno (oculto da API) 
    Name NVARCHAR(50) NOT NULL UNIQUE, -- resource-name (deve ser único) [cite: 50]
    CreationDate DATETIME DEFAULT GETDATE() NOT NULL -- creation-datetime 
);
GO

-- 3. Tabela: Container
-- Agrupa content-instances e subscriptions. Filho de Application [cite: 45, 46]
CREATE TABLE Container (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(50) NOT NULL, -- resource-name 
    CreationDate DATETIME DEFAULT GETDATE() NOT NULL, -- creation-datetime
    ParentAppId INT NOT NULL, -- Chave estrangeira para Application
    
    -- Garante que não há dois containers com o mesmo nome na mesma aplicação
    CONSTRAINT UQ_Container_Name_App UNIQUE(Name, ParentAppId),
    CONSTRAINT FK_Container_Application FOREIGN KEY (ParentAppId) REFERENCES Application(Id) ON DELETE CASCADE
);
GO

-- 4. Tabela: ContentInstance (Dados)
-- Representa os registos de dados. Filho de Container [cite: 47]
CREATE TABLE ContentInstance (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(50) NOT NULL, -- resource-name 
    CreationDate DATETIME DEFAULT GETDATE() NOT NULL, -- creation-datetime
    ParentContainerId INT NOT NULL, -- Chave estrangeira para Container
    
    Content NVARCHAR(MAX) NOT NULL, -- O conteúdo em si (XML, JSON, Texto) [cite: 48]
    ContentType NVARCHAR(50) NOT NULL, -- ex: "application/json" [cite: 48]

    -- Garante unicidade do nome dentro do contentor
    CONSTRAINT UQ_ContentInstance_Name_Cont UNIQUE(Name, ParentContainerId),
    CONSTRAINT FK_ContentInstance_Container FOREIGN KEY (ParentContainerId) REFERENCES Container(Id) ON DELETE CASCADE
);
GO

-- 5. Tabela: Subscription (Notificações)
-- Mecanismo de notificação. Filho de Container [cite: 49]
CREATE TABLE Subscription (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(50) NOT NULL, -- resource-name 
    CreationDate DATETIME DEFAULT GETDATE() NOT NULL, -- creation-datetime
    ParentContainerId INT NOT NULL, -- Chave estrangeira para Container
    
    EventId INT NOT NULL, -- evt: 1 (criação) ou 2 (eliminação) [cite: 57, 80]
    Endpoint NVARCHAR(255) NOT NULL, -- URL HTTP ou endereço MQTT [cite: 57, 81]

    -- Validação para garantir que o evento é apenas 1 ou 2
    CONSTRAINT CK_Subscription_Event CHECK (EventId IN (1, 2)),

    -- Garante unicidade do nome dentro do contentor
    CONSTRAINT UQ_Subscription_Name_Cont UNIQUE(Name, ParentContainerId),
    CONSTRAINT FK_Subscription_Container FOREIGN KEY (ParentContainerId) REFERENCES Container(Id) ON DELETE CASCADE
);
GO
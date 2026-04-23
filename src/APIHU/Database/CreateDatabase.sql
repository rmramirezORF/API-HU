-- =====================================================
-- Script de creación de base de datos para API-HU v2.0
-- =====================================================

-- Crear la base de datos si no existe
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'APIHU')
BEGIN
    CREATE DATABASE APIHU;
    PRINT 'Base de datos APIHU creada correctamente';
END
ELSE
BEGIN
    PRINT 'La base de datos APIHU ya existe';
END
GO

USE APIHU;
GO

-- =====================================================
-- Tabla: GeneracionesHU (NUEVA - v2.0)
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GeneracionesHU]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[GeneracionesHU](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [TextoEntrada] [nvarchar](10000) NOT NULL,
        [TextoProcesado] [nvarchar](15000) NULL,
        [Proyecto] [nvarchar](100) NULL,
        [Idioma] [nvarchar](10) NOT NULL DEFAULT 'es',
        [TotalHUs] [int] NOT NULL DEFAULT 0,
        [Exitoso] [bit] NOT NULL DEFAULT 0,
        [MensajeError] [nvarchar](500) NULL,
        [PromptVersion] [nvarchar](20) NULL,
        [FechaCreacion] [datetime] NOT NULL DEFAULT GETUTCDATE(),
        [FechaModificacion] [datetime] NULL,
        CONSTRAINT [PK_GeneracionesHU] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT 'Tabla GeneracionesHU creada correctamente';
END
ELSE
BEGIN
    PRINT 'La tabla GeneracionesHU ya existe';
END
GO
-- Tabla: HistoriasUsuario
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[HistoriasUsuario]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[HistoriasUsuario](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [Titulo] [nvarchar](200) NOT NULL,
        [Como] [nvarchar](100) NOT NULL,
        [Quiero] [nvarchar](200) NOT NULL,
        [Para] [nvarchar](300) NOT NULL,
        [Descripcion] [nvarchar](2000) NULL,
        [FechaCreacion] [datetime] NOT NULL DEFAULT GETUTCDATE(),
        [FechaModificacion] [datetime] NULL,
        CONSTRAINT [PK_HistoriasUsuario] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT 'Tabla HistoriasUsuario creada correctamente';
END
ELSE
BEGIN
    PRINT 'La tabla HistoriasUsuario ya existe';
END
GO

-- =====================================================
-- Tabla: CriteriosAceptacion
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CriteriosAceptacion]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CriteriosAceptacion](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [HistoriaUsuarioId] [int] NOT NULL,
        [Descripcion] [nvarchar](500) NOT NULL,
        [Orden] [int] NOT NULL,
        [EsObligatorio] [bit] NOT NULL DEFAULT 1,
        CONSTRAINT [PK_CriteriosAceptacion] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_CriteriosAceptacion_HistoriasUsuario] FOREIGN KEY ([HistoriaUsuarioId]) 
            REFERENCES [dbo].[HistoriasUsuario] ([Id]) ON DELETE CASCADE
    );
    PRINT 'Tabla CriteriosAceptacion creada correctamente';
END
ELSE
BEGIN
    PRINT 'La tabla CriteriosAceptacion ya existe';
END
GO

-- =====================================================
-- Tabla: TareasTecnicas
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TareasTecnicas]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[TareasTecnicas](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [HistoriaUsuarioId] [int] NOT NULL,
        [Descripcion] [nvarchar](300) NOT NULL,
        [Tipo] [nvarchar](50) NULL,
        [Orden] [int] NOT NULL,
        [EstaCompletada] [bit] NOT NULL DEFAULT 0,
        CONSTRAINT [PK_TareasTecnicas] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_TareasTecnicas_HistoriasUsuario] FOREIGN KEY ([HistoriaUsuarioId]) 
            REFERENCES [dbo].[HistoriasUsuario] ([Id]) ON DELETE CASCADE
    );
    PRINT 'Tabla TareasTecnicas creada correctamente';
END
ELSE
BEGIN
    PRINT 'La tabla TareasTecnicas ya existe';
END
GO

-- =====================================================
-- Índices para mejorar rendimiento
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_CriteriosAceptacion_HistoriaUsuarioId' AND object_id = OBJECT_ID('CriteriosAceptacion'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_CriteriosAceptacion_HistoriaUsuarioId] 
        ON [dbo].[CriteriosAceptacion] ([HistoriaUsuarioId]);
    PRINT 'Índice IX_CriteriosAceptacion_HistoriaUsuarioId creado';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TareasTecnicas_HistoriaUsuarioId' AND object_id = OBJECT_ID('TareasTecnicas'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TareasTecnicas_HistoriaUsuarioId] 
        ON [dbo].[TareasTecnicas] ([HistoriaUsuarioId]);
    PRINT 'Índice IX_TareasTecnicas_HistoriaUsuarioId creado';
END
GO

-- =====================================================
-- Verificar estructura
-- =====================================================
SELECT 
    t.name AS Tabla,
    c.name AS Columna,
    ty.name AS TipoDato,
    c.max_length AS Longitud,
    CASE WHEN c.is_nullable = 0 THEN 'NO' ELSE 'SI' END AS PermiteNulos,
    CASE WHEN pk.column_id IS NOT NULL THEN 'PK' ELSE '' END AS EsClave
FROM sys.tables t
INNER JOIN sys.columns c ON t.object_id = c.object_id
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
LEFT JOIN (
    SELECT ic.object_id, ic.column_id
    FROM sys.index_columns ic
    INNER JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    WHERE i.is_primary_key = 1
) pk ON c.object_id = pk.object_id AND c.column_id = pk.column_id
WHERE t.name IN ('HistoriasUsuario', 'CriteriosAceptacion', 'TareasTecnicas')
ORDER BY t.name, c.column_id;
GO

PRINT '==============================================';
PRINT 'Script completado exitosamente';
PRINT '==============================================';
GO
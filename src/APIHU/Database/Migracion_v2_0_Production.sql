-- =====================================================
-- Script de migración para agregar campos de producción v2.0
-- Ejecutar este script para actualizar la tabla GeneracionesHU
-- =====================================================

USE APIHU;
GO

PRINT '==============================================';
PRINT 'Iniciando migración v2.0 - Campos de producción';
PRINT '==============================================';
GO

-- =====================================================
-- Agregar columna Estado (si no existe)
-- =====================================================
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('GeneracionesHU') 
    AND name = 'Estado'
)
BEGIN
    ALTER TABLE [dbo].[GeneracionesHU] ADD [Estado] [int] NOT NULL DEFAULT 0;
    PRINT 'Columna Estado agregada correctamente';
END
ELSE
BEGIN
    PRINT 'La columna Estado ya existe';
END
GO

-- =====================================================
-- Agregar columna DuracionMs (si no existe)
-- =====================================================
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('GeneracionesHU') 
    AND name = 'DuracionMs'
)
BEGIN
    ALTER TABLE [dbo].[GeneracionesHU] ADD [DuracionMs] [int] NOT NULL DEFAULT 0;
    PRINT 'Columna DuracionMs agregada correctamente';
END
ELSE
BEGIN
    PRINT 'La columna DuracionMs ya existe';
END
GO

-- =====================================================
-- Agregar columna ModeloIA (si no existe)
-- =====================================================
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('GeneracionesHU') 
    AND name = 'ModeloIA'
)
BEGIN
    ALTER TABLE [dbo].[GeneracionesHU] ADD [ModeloIA] [nvarchar](50) NULL;
    PRINT 'Columna ModeloIA agregada correctamente';
END
ELSE
BEGIN
    PRINT 'La columna ModeloIA ya existe';
END
GO

-- =====================================================
-- Agregar columna TokensConsumidos (si no existe)
-- =====================================================
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('GeneracionesHU') 
    AND name = 'TokensConsumidos'
)
BEGIN
    ALTER TABLE [dbo].[GeneracionesHU] ADD [TokensConsumidos] [int] NULL;
    PRINT 'Columna TokensConsumidos agregada correctamente';
END
ELSE
BEGIN
    PRINT 'La columna TokensConsumidos ya existe';
END
GO

-- =====================================================
-- Agregar columna CorrelationId (si no existe)
-- =====================================================
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('GeneracionesHU') 
    AND name = 'CorrelationId'
)
BEGIN
    ALTER TABLE [dbo].[GeneracionesHU] ADD [CorrelationId] [nvarchar](50) NULL;
    PRINT 'Columna CorrelationId agregada correctamente';
END
ELSE
BEGIN
    PRINT 'La columna CorrelationId ya existe';
END
GO

-- =====================================================
-- Agregar columna ClientIP (si no existe)
-- =====================================================
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('GeneracionesHU') 
    AND name = 'ClientIP'
)
BEGIN
    ALTER TABLE [dbo].[GeneracionesHU] ADD [ClientIP] [nvarchar](50) NULL;
    PRINT 'Columna ClientIP agregada correctamente';
END
ELSE
BEGIN
    PRINT 'La columna ClientIP ya existe';
END
GO

-- =====================================================
-- Agregar columna UserAgent (si no existe)
-- =====================================================
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('GeneracionesHU') 
    AND name = 'UserAgent'
)
BEGIN
    ALTER TABLE [dbo].[GeneracionesHU] ADD [UserAgent] [nvarchar](500) NULL;
    PRINT 'Columna UserAgent agregada correctamente';
END
ELSE
BEGIN
    PRINT 'La columna UserAgent ya existe';
END
GO

-- =====================================================
-- Agregar índice para CorrelationId (si no existe)
-- =====================================================
IF NOT EXISTS (
    SELECT * FROM sys.indexes 
    WHERE name = 'IX_GeneracionesHU_CorrelationId' 
    AND object_id = OBJECT_ID('GeneracionesHU')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_GeneracionesHU_CorrelationId] 
        ON [dbo].[GeneracionesHU] ([CorrelationId]);
    PRINT 'Índice IX_GeneracionesHU_CorrelationId creado';
END
ELSE
BEGIN
    PRINT 'El índice IX_GeneracionesHU_CorrelationId ya existe';
END
GO

-- =====================================================
-- Agregar índice para Estado (si no existe)
-- =====================================================
IF NOT EXISTS (
    SELECT * FROM sys.indexes 
    WHERE name = 'IX_GeneracionesHU_Estado' 
    AND object_id = OBJECT_ID('GeneracionesHU')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_GeneracionesHU_Estado] 
        ON [dbo].[GeneracionesHU] ([Estado]);
    PRINT 'Índice IX_GeneracionesHU_Estado creado';
END
ELSE
BEGIN
    PRINT 'El índice IX_GeneracionesHU_Estado ya existe';
END
GO

-- =====================================================
-- Agregar índice para FechaCreacion (si no existe)
-- =====================================================
IF NOT EXISTS (
    SELECT * FROM sys.indexes 
    WHERE name = 'IX_GeneracionesHU_FechaCreacion' 
    AND object_id = OBJECT_ID('GeneracionesHU')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_GeneracionesHU_FechaCreacion] 
        ON [dbo].[GeneracionesHU] ([FechaCreacion] DESC);
    PRINT 'Índice IX_GeneracionesHU_FechaCreacion creado';
END
ELSE
BEGIN
    PRINT 'El índice IX_GeneracionesHU_FechaCreacion ya existe';
END
GO

-- =====================================================
-- Agregar foreign key a HistoriasUsuario (si no existe)
-- =====================================================
IF NOT EXISTS (
    SELECT * FROM sys.foreign_keys 
    WHERE name = 'FK_HistoriasUsuario_GeneracionesHU'
)
BEGIN
    ALTER TABLE [dbo].[HistoriasUsuario] ADD [GeneracionHUId] [int] NULL;
    
    ALTER TABLE [dbo].[HistoriasUsuario] ADD CONSTRAINT [FK_HistoriasUsuario_GeneracionesHU] 
        FOREIGN KEY ([GeneracionHUId]) REFERENCES [dbo].[GeneracionesHU] ([Id]) ON DELETE SET NULL;
    
    PRINT 'Foreign Key FK_HistoriasUsuario_GeneracionesHU creada correctamente';
END
ELSE
BEGIN
    PRINT 'La Foreign Key FK_HistoriasUsuario_GeneracionesHU ya existe';
END
GO

-- =====================================================
-- Verificar estructura actualizada
-- =====================================================
PRINT '';
PRINT '==============================================';
PRINT 'Estructura actualizada de GeneracionesHU:';
PRINT '==============================================';
GO

SELECT 
    c.name AS Columna,
    ty.name AS TipoDato,
    c.max_length AS Longitud,
    CASE WHEN c.is_nullable = 0 THEN 'NO' ELSE 'SI' END AS PermiteNulos,
    CASE WHEN dc.definition IS NOT NULL THEN dc.definition ELSE '' END AS DefaultValue
FROM sys.columns c
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
LEFT JOIN sys.default_constraints dc ON c.default_object_id = dc.object_id
WHERE c.object_id = OBJECT_ID('GeneracionesHU')
ORDER BY c.column_id;
GO

PRINT '';
PRINT '==============================================';
PRINT 'Migración completada exitosamente';
PRINT '==============================================';
GO
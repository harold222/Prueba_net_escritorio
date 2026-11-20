CREATE TABLE dbo.SchemaVersion (
    VersionNumber int NOT NULL PRIMARY KEY,
    AppliedUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.Pacientes (
    PacienteId int IDENTITY PRIMARY KEY,
    TipoDocumento varchar(20) NOT NULL,
    NumeroDocumento varchar(40) NOT NULL,
    Nombre nvarchar(160) NOT NULL,
    FechaNacimiento date NULL,
    Activo bit NOT NULL DEFAULT 1,
    CreadoUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Paciente_Documento UNIQUE (TipoDocumento, NumeroDocumento)
);

CREATE TABLE dbo.ServiciosMedicos (
    ServicioMedicoId int IDENTITY PRIMARY KEY,
    Codigo varchar(30) NOT NULL UNIQUE,
    Nombre nvarchar(160) NOT NULL,
    Activo bit NOT NULL DEFAULT 1,
    CreadoUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.Atenciones (
    AtencionId int IDENTITY PRIMARY KEY,
    PacienteId int NOT NULL REFERENCES dbo.Pacientes (PacienteId),
    FechaAtencion date NOT NULL,
    RegistradaUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    OperacionId uniqueidentifier NOT NULL UNIQUE
);

CREATE TABLE dbo.AtencionServicios (
    AtencionServicioId int IDENTITY PRIMARY KEY,
    AtencionId int NOT NULL,
    PacienteId int NOT NULL,
    FechaAtencion date NOT NULL,
    ServicioMedicoId int NOT NULL REFERENCES dbo.ServiciosMedicos (ServicioMedicoId),
    CreadoUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_AtencionServicio UNIQUE (PacienteId, FechaAtencion, ServicioMedicoId),
    CONSTRAINT UQ_AtencionServicio_Cabecera UNIQUE (AtencionId, ServicioMedicoId),
    CONSTRAINT FK_Detalle_Cabecera FOREIGN KEY (AtencionId) REFERENCES dbo.Atenciones (AtencionId),
    CONSTRAINT FK_Detalle_Paciente FOREIGN KEY (PacienteId) REFERENCES dbo.Pacientes (PacienteId)
);

CREATE TABLE dbo.AuditoriaAtenciones (
    AuditoriaId bigint IDENTITY PRIMARY KEY,
    FechaUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    Operacion varchar(50) NOT NULL,
    Resultado varchar(40) NOT NULL,
    AtencionId int NULL,
    PacienteId int NULL,
    OperacionId uniqueidentifier NULL,
    Estacion nvarchar(128) NULL,
    CONSTRAINT CK_Auditoria_Resultado CHECK (Resultado <> '')
);

CREATE INDEX IX_Pacientes_Busqueda ON dbo.Pacientes (NumeroDocumento, Nombre);
CREATE INDEX IX_Servicios_Activos ON dbo.ServiciosMedicos (Activo, Nombre);
CREATE INDEX IX_Atenciones_Historial ON dbo.Atenciones (PacienteId, FechaAtencion);
CREATE INDEX IX_Auditoria_Fecha ON dbo.AuditoriaAtenciones (FechaUtc, Operacion, Resultado);

INSERT dbo.SchemaVersion (VersionNumber) VALUES (1);

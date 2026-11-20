IF NOT EXISTS (SELECT 1 FROM dbo.Pacientes WHERE TipoDocumento = 'CC' AND NumeroDocumento = '100000001')
    INSERT dbo.Pacientes (TipoDocumento, NumeroDocumento, Nombre)
    VALUES ('CC', '100000001', 'Paciente demo');

IF NOT EXISTS (SELECT 1 FROM dbo.Pacientes WHERE TipoDocumento = 'CC' AND NumeroDocumento = '100000002')
    INSERT dbo.Pacientes (TipoDocumento, NumeroDocumento, Nombre, Activo)
    VALUES ('CC', '100000002', 'Paciente demo inactivo', 0);

IF NOT EXISTS (SELECT 1 FROM dbo.ServiciosMedicos WHERE Codigo = 'CONS')
    INSERT dbo.ServiciosMedicos (Codigo, Nombre) VALUES ('CONS', 'Consulta general');

IF NOT EXISTS (SELECT 1 FROM dbo.ServiciosMedicos WHERE Codigo = 'INAC')
    INSERT dbo.ServiciosMedicos (Codigo, Nombre, Activo) VALUES ('INAC', 'Servicio inactivo', 0);

IF NOT EXISTS (SELECT 1 FROM dbo.ServiciosMedicos WHERE Codigo = 'CTRL')
    INSERT dbo.ServiciosMedicos (Codigo, Nombre) VALUES ('CTRL', 'Consulta de control');

USE master;
GO
IF SUSER_ID(N'ips_atenciones_app') IS NULL
BEGIN
    DECLARE @password nvarchar(128) = CONVERT(nvarchar(128), 0x$(AppPasswordHex));
    DECLARE @createLogin nvarchar(400) = CONCAT(
        N'CREATE LOGIN [ips_atenciones_app] WITH PASSWORD',
        N' = ', QUOTENAME(@password, ''''), N', CHECK_POLICY = ON');
    EXEC sys.sp_executesql @createLogin;
END;
GO
USE IpsAtenciones;
GO
IF USER_ID(N'ips_atenciones_app') IS NULL
    CREATE USER [ips_atenciones_app] FOR LOGIN [ips_atenciones_app];
GO
GRANT REFERENCES ON TYPE::dbo.AtencionServicioIdList TO [ips_atenciones_app];
GRANT EXECUTE ON TYPE::dbo.AtencionServicioIdList TO [ips_atenciones_app];
GRANT EXECUTE ON OBJECT::dbo.usp_Paciente_Listar TO [ips_atenciones_app];
GRANT EXECUTE ON OBJECT::dbo.usp_Paciente_Crear TO [ips_atenciones_app];
GRANT EXECUTE ON OBJECT::dbo.usp_Paciente_Actualizar TO [ips_atenciones_app];
GRANT EXECUTE ON OBJECT::dbo.usp_Paciente_Desactivar TO [ips_atenciones_app];
GRANT EXECUTE ON OBJECT::dbo.usp_ServicioMedico_Listar TO [ips_atenciones_app];
GRANT EXECUTE ON OBJECT::dbo.usp_ServicioMedico_ListarActivos TO [ips_atenciones_app];
GRANT EXECUTE ON OBJECT::dbo.usp_ServicioMedico_Crear TO [ips_atenciones_app];
GRANT EXECUTE ON OBJECT::dbo.usp_ServicioMedico_Actualizar TO [ips_atenciones_app];
GRANT EXECUTE ON OBJECT::dbo.usp_ServicioMedico_Desactivar TO [ips_atenciones_app];
GRANT EXECUTE ON OBJECT::dbo.usp_Atencion_Registrar TO [ips_atenciones_app];
GRANT EXECUTE ON OBJECT::dbo.usp_Atencion_ConsultarPorOperacion TO [ips_atenciones_app];
GRANT EXECUTE ON OBJECT::dbo.usp_Atencion_Listar TO [ips_atenciones_app];
GO

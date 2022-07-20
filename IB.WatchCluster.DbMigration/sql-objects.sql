/*Device Info*/

IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DeviceInfoSequence]') AND type in (N'SO'))
DROP SEQUENCE DeviceInfoSequence 
GO

CREATE SEQUENCE DeviceInfoSequence  
    START WITH 25000000  
    INCREMENT BY 1
GO

IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DeviceInfo]') AND type in (N'U'))
DROP TABLE [dbo].[DeviceInfo]
GO

CREATE TABLE [dbo].[DeviceInfo](
	[ID] [bigint] NULL,
	[DeviceID] [nvarchar](100) NULL,
	[DeviceName] [nvarchar](100) NULL,
	[FirstRequestTime] [datetime] DEFAULT (GETUTCDATE()),
	[FirstRequestDate]  AS (CONVERT([date],[FirstRequestTime])) PERSISTED
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[DeviceInfo] ADD  DEFAULT (NEXT VALUE FOR [DeviceInfoSequence]) FOR [ID]
GO

CREATE UNIQUE CLUSTERED INDEX [IXUC_ID] ON [dbo].[DeviceInfo] ([ID] ASC) 
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = ON) ON [PRIMARY]
GO

CREATE UNIQUE NONCLUSTERED INDEX [IXU_DeviceID] ON [dbo].[DeviceInfo] ([DeviceID] ASC)
GO

CREATE NONCLUSTERED INDEX [IX_RequestDate] ON [dbo].[DeviceInfo]([FirstRequestDate] ASC)
GO

/* Request Info*/
IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProcessingLogSequence]') AND type in (N'SO'))
DROP SEQUENCE ProcessingLogSequence 
GO

CREATE SEQUENCE ProcessingLogSequence  
    START WITH 25000000  
    INCREMENT BY 1
GO

IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProcessingLog]') AND type in (N'U'))
DROP TABLE [dbo].[ProcessingLog]
GO

CREATE TABLE [dbo].[ProcessingLog](
	[ID] [bigint] NULL,
	[DeviceInfoId] [bigint] NULL,
	[RequestTime] [datetime] NULL DEFAULT (GETUTCDATE()),
	[CityName] [nvarchar](100) NULL,
	[Lat] [decimal](20, 10) NULL,
	[Lon] [decimal](20, 10) NULL,
	[FaceVersion] [nvarchar](50) NULL,
	[FrameworkVersion] [nvarchar](50) NULL,
	[CIQVersion] [nvarchar](50) NULL,
	[RequestType] [nvarchar](50) NULL,
	[Temperature] [decimal](20, 10) NULL,
	[Wind] [decimal](20, 10) NULL,
	[PrecipProbability] [decimal](20, 10) NULL,
	[BaseCurrency] [nvarchar](10) NULL,
	[TargetCurrency] [nvarchar](10) NULL,
	[ExchangeRate] [decimal](20, 10) NULL,
	[RequestId] nvarchar(30) NULL,
	[RequestDate]  AS (CONVERT([date],[RequestTime])) PERSISTED
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[ProcessingLog] ADD  DEFAULT (NEXT VALUE FOR [ProcessingLogSequence]) FOR [id]
GO

CREATE CLUSTERED INDEX [IXC_DeviceInfoId] ON [dbo].[ProcessingLog] ([DeviceInfoId] ASC)
GO

CREATE NONCLUSTERED INDEX [IX_RequestDateDeviceID] ON [dbo].[ProcessingLog] ([RequestDate] ASC, [DeviceInfoId] ASC)
GO

CREATE NONCLUSTERED INDEX [IX_RequestDate] ON [dbo].[ProcessingLog] ([RequestDate] ASC)
GO

CREATE UNIQUE INDEX [IXU_Id] ON [dbo].[ProcessingLog] ([ID])
GO



/* insert procedure*/

CREATE PROCEDURE [dbo].[AddDevice] @DeviceID nvarchar(100), @DeviceName nvarchar(100)
AS

	SET NOCOUNT ON;

	MERGE INTO [dbo].[DeviceInfo] with (holdlock) D  
	USING (SELECT @DeviceID, @DeviceName) s([DeviceID], [DeviceName]) 
	ON D.DeviceID = s.DeviceID
	WHEN MATCHED THEN
		UPDATE SET DeviceID = D.DeviceID
	WHEN not matched THEN 
		INSERT (DeviceID, DeviceName) VALUES (s.[DeviceID], s.[DeviceName])
	OUTPUT inserted.*;
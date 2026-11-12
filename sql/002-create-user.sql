CREATE TABLE [dbo].[USER](
	[IdUser] [int] IDENTITY(1,1) NOT NULL,
	[singleSignOnLogin] [varchar](100) NOT NULL,
	[PASSUSER] [varchar](255) NOT NULL,
 CONSTRAINT [PK_USER] PRIMARY KEY CLUSTERED ([IdUser] ASC),
 CONSTRAINT [UQ_USER_singleSignOnLogin] UNIQUE ([singleSignOnLogin])
);

INSERT INTO [dbo].[USER] ([singleSignOnLogin], [PASSUSER])
VALUES ('admin', 'admin');

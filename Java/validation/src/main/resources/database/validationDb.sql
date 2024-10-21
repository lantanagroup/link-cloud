/****** Object:  Table [dbo].[artifact]    Script Date: 9/24/2024 12:36:05 PM ******/
IF OBJECT_ID('dbo.artifact', 'U') IS NULL
BEGIN
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[artifact](
    [id] [bigint] NOT NULL,
    [content] [varbinary](max) NULL,
    [name] [varchar](255) NULL,
    [type] [varchar](255) NULL,
    PRIMARY KEY CLUSTERED
(
[id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
    GO
END;
GO


/****** Object:  Table [dbo].[category]    Script Date: 9/24/2024 12:36:05 PM ******/
IF OBJECT_ID('dbo.category', 'U') IS NULL
BEGIN
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
CREATE TABLE [dbo].[category](
    [id] [varchar](255) NOT NULL,
    [acceptable] [bit] NOT NULL,
    [guidance] [varchar](255) NOT NULL,
    [require_all_rule_sets] [bit] NULL,
    PRIMARY KEY CLUSTERED
(
[id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY]
    GO
END;
GO


/****** Object:  Table [dbo].[category_rulesets]    Script Date: 9/24/2024 12:36:05 PM ******/
IF OBJECT_ID('dbo.category_rulesets', 'U') IS NULL
BEGIN
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
CREATE TABLE [dbo].[category_rulesets](
    [id] [bigint] NOT NULL,
    [created] [datetime2](6) NOT NULL,
    [require_all_rule_sets] [bit] NOT NULL,
    [rule_sets] [varchar](max) NOT NULL,
    [version] [int] NOT NULL,
    [category_id] [varchar](255) NOT NULL,
    PRIMARY KEY CLUSTERED
(
[id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
    CONSTRAINT [UK58wt1k3651gmqs2goreur2s4c] UNIQUE NONCLUSTERED
(
    [category_id] ASC,
[version] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
    GO
END;
GO


/****** Object:  Table [dbo].[result]    Script Date: 9/24/2024 12:36:05 PM ******/
IF OBJECT_ID('dbo.result', 'U') IS NULL
BEGIN
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
CREATE TABLE [dbo].[result](
    [id] [bigint] NOT NULL,
    [expression] [varchar](4096) NOT NULL,
    [location] [varchar](255) NULL,
    [message] [varchar](max) NOT NULL,
    [report_id] [varchar](255) NOT NULL,
    [severity] [smallint] NOT NULL,
    [tenant_id] [varchar](255) NOT NULL,
    [type] [smallint] NULL,
    PRIMARY KEY CLUSTERED
(
[id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
    GO
END;
GO


/****** Object:  Table [dbo].[validation_result]    Script Date: 9/24/2024 12:36:05 PM ******/
IF OBJECT_ID('dbo.validation_result', 'U') IS NULL
BEGIN
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
CREATE TABLE [dbo].[validation_result](
    [id] [bigint] NOT NULL,
    [expression] [varchar](4096) NOT NULL,
    [message] [varchar](max) NOT NULL,
    [report_id] [varchar](255) NOT NULL,
    [severity] [smallint] NOT NULL,
    [tenant_id] [varchar](255) NOT NULL,
    PRIMARY KEY CLUSTERED
(
[id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
    GO
END;
GO

IF OBJECT_ID('dbo.category_rulesets', 'U') IS NOT NULL
BEGIN
ALTER TABLE [dbo].[category_rulesets]  WITH CHECK ADD  CONSTRAINT [FKs9i8oqxq7or7ajcwk7s38f4gy] FOREIGN KEY([category_id])
    REFERENCES [dbo].[category] ([id])
END;
GO

IF OBJECT_ID('dbo.category_rulesets', 'U') IS NOT NULL
BEGIN
ALTER TABLE [dbo].[category_rulesets] CHECK CONSTRAINT [FKs9i8oqxq7or7ajcwk7s38f4gy]
END;
GO

IF OBJECT_ID('dbo.artifact', 'U') IS NOT NULL
BEGIN
ALTER TABLE [dbo].[artifact]  WITH CHECK ADD CHECK  (([type]='RESOURCE' OR [type]='PACKAGE'))
GO
ALTER TABLE [dbo].[result]  WITH CHECK ADD CHECK  (([severity]>=(0) AND [severity]<=(4)))
    GO
ALTER TABLE [dbo].[result]  WITH CHECK ADD CHECK  (([type]>=(0) AND [type]<=(31)))
    GO
END;
GO

IF OBJECT_ID('dbo.validation_result', 'U') IS NOT NULL
BEGIN
ALTER TABLE [dbo].[validation_result]  WITH CHECK ADD CHECK  (([severity]>=(0) AND [severity]<=(4)))
END;
GO

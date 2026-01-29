CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

CREATE TABLE "Users" (
    "Id" uuid NOT NULL,
    "Email" character varying(255) NOT NULL,
    "FirstName" character varying(100) NOT NULL,
    "LastName" character varying(100) NOT NULL,
    "PasswordHash" character varying(500) NOT NULL,
    "Role" character varying(50) NOT NULL,
    "IsActive" boolean NOT NULL,
    "LastLoginAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
);

CREATE TABLE "TaskRequests" (
    "Id" uuid NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Description" character varying(2000) NOT NULL,
    "Priority" character varying(50) NOT NULL,
    "Status" character varying(50) NOT NULL,
    "DueDate" timestamp with time zone,
    "CompletedAt" timestamp with time zone,
    "CreatedByUserId" uuid NOT NULL,
    "AssignedToUserId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_TaskRequests" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_TaskRequests_Users_AssignedToUserId" FOREIGN KEY ("AssignedToUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_TaskRequests_Users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "TaskComments" (
    "Id" uuid NOT NULL,
    "Content" character varying(1000) NOT NULL,
    "TaskRequestId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_TaskComments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_TaskComments_TaskRequests_TaskRequestId" FOREIGN KEY ("TaskRequestId") REFERENCES "TaskRequests" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_TaskComments_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_TaskComments_TaskRequestId" ON "TaskComments" ("TaskRequestId");

CREATE INDEX "IX_TaskComments_UserId" ON "TaskComments" ("UserId");

CREATE INDEX "IX_TaskRequests_AssignedToUserId" ON "TaskRequests" ("AssignedToUserId");

CREATE INDEX "IX_TaskRequests_CreatedByUserId" ON "TaskRequests" ("CreatedByUserId");

CREATE INDEX "IX_TaskRequests_DueDate" ON "TaskRequests" ("DueDate");

CREATE INDEX "IX_TaskRequests_Status" ON "TaskRequests" ("Status");

CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260128074914_InitialCreate', '8.0.20');

COMMIT;

START TRANSACTION;

ALTER TABLE "Users" ADD "PhoneNumber" character varying(20);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260129031146_AddPhoneNumberToUser', '8.0.20');

COMMIT;


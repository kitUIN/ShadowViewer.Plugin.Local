-- Generated from the pre-migration entities using SqlSugarCore 5.1.4.211. Synthetic data only.
BEGIN TRANSACTION;
CREATE TABLE "CacheImg"(
"Id" bigint NOT NULL PRIMARY KEY ,
"Md5" Nchar(32) NOT NULL  ,
"Dir" Text NOT NULL  ,
"Path" varchar(255) NOT NULL  ,
"ComicId" bigint NOT NULL    );
INSERT INTO "CacheImg" VALUES(700000000000000006,'legacy-image','C:/legacy/thumb','C:/legacy/thumb\700000000000000006.png',700000000000000003);
CREATE TABLE "CacheZip"(
"Md5" Nvarchar(255) NOT NULL  ,
"Sha1" Nvarchar(255) NOT NULL  ,
"Password" Nvarchar(255) NULL  ,
"Name" Nvarchar(1000) NOT NULL  ,
"CachePath" TEXT NULL  ,
"ComicId" bigint NULL   ,
 Primary key("Md5","Sha1") );
INSERT INTO "CacheZip" VALUES('legacy-md5','legacy-sha1','legacy-password','legacy comic','C:/legacy/book',700000000000000003);
CREATE TABLE "ComicChapter"(
"Id" bigint NOT NULL PRIMARY KEY ,
"Name" Nvarchar(2048) NOT NULL  ,
"Order" integer NOT NULL  ,
"ComicId" bigint NOT NULL  ,
"PageCount" integer NOT NULL  ,
"Size" bigint NOT NULL  ,
"CreatedDateTime" datetime NOT NULL    );
INSERT INTO "ComicChapter" VALUES(700000000000000004,'chapter',1,700000000000000003,10,0,'2026-10-09 15:29:09.2961627');
CREATE TABLE "ComicDetail"(
"ComicId" bigint NOT NULL PRIMARY KEY ,
"ChapterCount" integer NOT NULL   /*话-数量*/ ,
"PageCount" integer NOT NULL   /*页-数量*/ ,
"ProcessMode" varchar(32) NOT NULL   /*处理模式*/ ,
"StoragePath" TEXT NULL   /*存储路径*/ ,
"ExtendId" varchar(255) NULL   /*扩展Id*/ ,
"ExtendPath" TEXT NULL   /*扩展路径*/ ,
"Remark" TEXT NULL   /*备注*/   );
INSERT INTO "ComicDetail" VALUES(700000000000000003,1,10,'Zip','C:/legacy/book',NULL,NULL,NULL);
CREATE TABLE "ComicNode"(
"Id" bigint NOT NULL PRIMARY KEY ,
"ParentId" bigint NOT NULL   DEFAULT '-1' /*父Id*/ ,
"NodeType" varchar(32) NOT NULL   /*节点类型*/ ,
"Name" Nvarchar(500) NOT NULL   /*名称*/ ,
"Thumb" TEXT NOT NULL   DEFAULT 'mx-appx:///default.png' /*缩略图*/ ,
"CreatedDateTime" datetime NOT NULL   /*创建时间*/ ,
"UpdatedDateTime" datetime NOT NULL   /*更新时间*/ ,
"SourcePluginDataId" varchar(255) NULL  ,
"IsFolder" bit NOT NULL  ,
"Size" bigint NOT NULL   /*文件大小*/ ,
"IsBroken" bit NOT NULL   /*是否损坏*/ ,
"BrokenReason" varchar(255) NULL   /*损坏原因*/ ,
"IsDelete" bit NOT NULL   /*是否删除*/   );
INSERT INTO "ComicNode" VALUES(-1,-2,'Folder','root','ms-appx:///Assets/Default/folder.png','2026-10-09 15:29:09','2026-10-09 15:29:09',NULL,1,0,0,NULL,0);
INSERT INTO "ComicNode" VALUES(700000000000000003,-1,'Comic','legacy comic','mx-appx:///default.png','2026-10-09 15:29:09','2026-10-09 15:29:09','local1.6.6',0,0,0,NULL,0);
CREATE TABLE "ComicPicture"(
"Id" bigint NOT NULL PRIMARY KEY ,
"ComicId" bigint NOT NULL  ,
"ChapterId" bigint NOT NULL  ,
"Name" Nvarchar(2048) NOT NULL  ,
"StoragePath" TEXT NULL   /*存储路径*/ ,
"Size" bigint NOT NULL  ,
"CreatedDateTime" datetime NOT NULL    );
INSERT INTO "ComicPicture" VALUES(700000000000000005,700000000000000003,700000000000000004,'1.png','C:/legacy/book/1.png',0,'2026-10-09 15:29:09.3060957');
CREATE TABLE "LocalAuthor"(
"Id" bigint NOT NULL PRIMARY KEY  /*Id*/ ,
"Name" varchar(255) NOT NULL   /*作者名称*/   );
INSERT INTO "LocalAuthor" VALUES(700000000000000002,'legacy author');
CREATE TABLE "LocalComicAuthorMapping"(
"ComicId" bigint NOT NULL  ,
"AuthorId" bigint NOT NULL    );
INSERT INTO "LocalComicAuthorMapping" VALUES(700000000000000003,700000000000000002);
CREATE TABLE "LocalComicTagMapping"(
"ComicId" bigint NOT NULL  ,
"TagId" bigint NOT NULL    );
INSERT INTO "LocalComicTagMapping" VALUES(700000000000000003,700000000000000001);
CREATE TABLE "LocalHistory"(
"Id" bigint NOT NULL PRIMARY KEY ,
"Title" varchar(255) NOT NULL  ,
"Thumb" TEXT NOT NULL  ,
"LastReadDateTime" datetime NOT NULL  ,
"Extra" varchar(255) NULL  ,
"PluginId" varchar(255) NOT NULL    );
INSERT INTO "LocalHistory" VALUES(700000000000000003,'legacy comic','mx-appx:///default.png','2026-10-09 15:29:09.3152046',NULL,'ShadowViewer.Plugin.Local');
CREATE TABLE "LocalReadingRecord"(
"Id" bigint NOT NULL PRIMARY KEY ,
"ExtraComicId" varchar(255) NULL   /*额外的漫画Id*/ ,
"Percent" decimal NOT NULL   /*阅读进度*/ ,
"LastPicture" integer NOT NULL   /*上次阅读-页*/ ,
"LastEpisode" integer NOT NULL   /*上次阅读-话*/ ,
"CreatedDateTime" datetime NOT NULL   /*创建时间*/ ,
"UpdatedDateTime" datetime NOT NULL   /*更新时间*/   );
INSERT INTO "LocalReadingRecord" VALUES(700000000000000003,NULL,42.5,4,1,'2026-10-09 15:29:09','2026-10-09 15:29:09');
CREATE TABLE "ShadowTag"(
"Id" bigint NOT NULL PRIMARY KEY ,
"Name" Nvarchar(255) NOT NULL  ,
"BackgroundHex" Nvarchar(9) NOT NULL  ,
"ForegroundHex" Nvarchar(9) NOT NULL  ,
"Icon" varchar(255) NULL   /*图标*/ ,
"PluginId" varchar(255) NOT NULL   /*图标*/ ,
"TagType" integer NOT NULL    );
INSERT INTO "ShadowTag" VALUES(700000000000000001,'legacy tag','#fff','#000',NULL,'local',1);
CREATE TABLE "SourcePluginData"(
"Id" varchar(255) NOT NULL PRIMARY KEY ,
"PluginId" varchar(255) NOT NULL  ,
"Name" varchar(255) NOT NULL  ,
"DataVersion" varchar(255) NOT NULL  ,
"BackgroundColorHex" varchar(20) NOT NULL  ,
"ForegroundColorHex" varchar(20) NOT NULL  ,
"ExtraData" varchar(4000) NULL    );
INSERT INTO "SourcePluginData" VALUES('local1.6.6','local','local reader','1.6.6','#fff','#000','{"origin":"legacy"}');
CREATE UNIQUE INDEX unique_shadow_tag_name ON `ShadowTag`(`Name` Asc);
CREATE INDEX index_comic_node_parent_id ON `ComicNode`(`ParentId` Asc);
CREATE INDEX index_comic_node_created_at ON `ComicNode`(`CreatedDateTime` Asc);
CREATE INDEX index_comic_node_updated_at ON `ComicNode`(`UpdatedDateTime` Asc);
CREATE INDEX index_comic_node_name ON `ComicNode`(`Name` Asc);
CREATE INDEX index_comic_node_type ON `ComicNode`(`NodeType` Asc);
COMMIT;

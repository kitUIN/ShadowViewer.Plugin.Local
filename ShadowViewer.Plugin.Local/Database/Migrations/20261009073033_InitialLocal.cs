using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShadowViewer.Plugin.Local.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialLocal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LocalAuthor",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalAuthor", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocalHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Thumb = table.Column<string>(type: "TEXT", nullable: false),
                    LastReadDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Extra = table.Column<string>(type: "TEXT", nullable: true),
                    PluginId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SourcePluginData",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    PluginId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    DataVersion = table.Column<string>(type: "TEXT", nullable: false),
                    BackgroundColorHex = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ForegroundColorHex = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ExtraData = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourcePluginData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComicNode",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ParentId = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: -1L),
                    NodeType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "Folder"),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Thumb = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "mx-appx:///default.png"),
                    CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SourcePluginDataId = table.Column<string>(type: "TEXT", nullable: true),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    IsBroken = table.Column<bool>(type: "INTEGER", nullable: false),
                    BrokenReason = table.Column<string>(type: "TEXT", nullable: true),
                    IsDelete = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFolder = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComicNode", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComicNode_SourcePluginData_SourcePluginDataId",
                        column: x => x.SourcePluginDataId,
                        principalTable: "SourcePluginData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CacheImg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Md5 = table.Column<string>(type: "TEXT", nullable: false),
                    Dir = table.Column<string>(type: "TEXT", nullable: false),
                    ComicId = table.Column<long>(type: "INTEGER", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CacheImg", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CacheImg_ComicNode_ComicId",
                        column: x => x.ComicId,
                        principalTable: "ComicNode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComicChapter",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    ComicId = table.Column<long>(type: "INTEGER", nullable: false),
                    PageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComicChapter", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComicChapter_ComicNode_ComicId",
                        column: x => x.ComicId,
                        principalTable: "ComicNode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComicDetail",
                columns: table => new
                {
                    ComicId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChapterCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessMode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "Folder"),
                    StoragePath = table.Column<string>(type: "TEXT", nullable: true),
                    ExtendId = table.Column<string>(type: "TEXT", nullable: true),
                    ExtendPath = table.Column<string>(type: "TEXT", nullable: true),
                    Remark = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComicDetail", x => x.ComicId);
                    table.ForeignKey(
                        name: "FK_ComicDetail_ComicNode_ComicId",
                        column: x => x.ComicId,
                        principalTable: "ComicNode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LocalReadingRecord",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    ExtraComicId = table.Column<string>(type: "TEXT", nullable: true),
                    Percent = table.Column<decimal>(type: "TEXT", nullable: false),
                    LastPicture = table.Column<int>(type: "INTEGER", nullable: false),
                    LastEpisode = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalReadingRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocalReadingRecord_ComicNode_Id",
                        column: x => x.Id,
                        principalTable: "ComicNode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComicPicture",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ComicId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChapterId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    StoragePath = table.Column<string>(type: "TEXT", nullable: true),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComicPicture", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComicPicture_ComicChapter_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "ComicChapter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ComicPicture_ComicNode_ComicId",
                        column: x => x.ComicId,
                        principalTable: "ComicNode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LocalComicAuthorMapping",
                columns: table => new
                {
                    ComicId = table.Column<long>(type: "INTEGER", nullable: false),
                    AuthorId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalComicAuthorMapping", x => new { x.ComicId, x.AuthorId });
                    table.ForeignKey(
                        name: "FK_LocalComicAuthorMapping_ComicDetail_ComicId",
                        column: x => x.ComicId,
                        principalTable: "ComicDetail",
                        principalColumn: "ComicId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LocalComicAuthorMapping_LocalAuthor_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "LocalAuthor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LocalComicTagMapping",
                columns: table => new
                {
                    ComicId = table.Column<long>(type: "INTEGER", nullable: false),
                    TagId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalComicTagMapping", x => new { x.ComicId, x.TagId });
                    table.ForeignKey(
                        name: "FK_LocalComicTagMapping_ComicDetail_ComicId",
                        column: x => x.ComicId,
                        principalTable: "ComicDetail",
                        principalColumn: "ComicId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LocalComicTagMapping_ShadowTag_TagId",
                        column: x => x.TagId,
                        principalTable: "ShadowTag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CacheImg_ComicId",
                table: "CacheImg",
                column: "ComicId");

            migrationBuilder.CreateIndex(
                name: "IX_ComicChapter_ComicId",
                table: "ComicChapter",
                column: "ComicId");

            migrationBuilder.CreateIndex(
                name: "index_comic_node_created_at",
                table: "ComicNode",
                column: "CreatedDateTime");

            migrationBuilder.CreateIndex(
                name: "index_comic_node_name",
                table: "ComicNode",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "index_comic_node_parent_id",
                table: "ComicNode",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "index_comic_node_type",
                table: "ComicNode",
                column: "NodeType");

            migrationBuilder.CreateIndex(
                name: "index_comic_node_updated_at",
                table: "ComicNode",
                column: "UpdatedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_ComicNode_SourcePluginDataId",
                table: "ComicNode",
                column: "SourcePluginDataId");

            migrationBuilder.CreateIndex(
                name: "IX_ComicPicture_ChapterId",
                table: "ComicPicture",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_ComicPicture_ComicId",
                table: "ComicPicture",
                column: "ComicId");

            migrationBuilder.CreateIndex(
                name: "IX_LocalComicAuthorMapping_AuthorId",
                table: "LocalComicAuthorMapping",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_LocalComicTagMapping_TagId",
                table: "LocalComicTagMapping",
                column: "TagId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CacheImg");

            migrationBuilder.DropTable(
                name: "ComicPicture");

            migrationBuilder.DropTable(
                name: "LocalComicAuthorMapping");

            migrationBuilder.DropTable(
                name: "LocalComicTagMapping");

            migrationBuilder.DropTable(
                name: "LocalHistory");

            migrationBuilder.DropTable(
                name: "LocalReadingRecord");

            migrationBuilder.DropTable(
                name: "ComicChapter");

            migrationBuilder.DropTable(
                name: "LocalAuthor");

            migrationBuilder.DropTable(
                name: "ComicDetail");

            migrationBuilder.DropTable(
                name: "ComicNode");

            migrationBuilder.DropTable(
                name: "SourcePluginData");
        }
    }
}

﻿// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using blizzardCrawler.db;

#nullable disable

namespace blizzardCrawler.db.Migrations
{
    [DbContext(typeof(BlContext))]
    [Migration("20230811124336_Init")]
    partial class Init
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.10")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("blizzardCrawler.db.MatchInfo", b =>
                {
                    b.Property<int>("MatchInfoId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<int>("Decision")
                        .HasColumnType("int");

                    b.Property<long>("MatchDateUnixTimestamp")
                        .HasColumnType("bigint");

                    b.Property<int>("PlayerId")
                        .HasColumnType("int");

                    b.Property<int>("Region")
                        .HasColumnType("int");

                    b.HasKey("MatchInfoId");

                    b.HasIndex("MatchDateUnixTimestamp");

                    b.HasIndex("PlayerId", "MatchDateUnixTimestamp", "Region", "Decision")
                        .IsUnique();

                    b.ToTable("MatchInfos");
                });

            modelBuilder.Entity("blizzardCrawler.db.Player", b =>
                {
                    b.Property<int>("PlayerId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(30)
                        .HasColumnType("varchar(30)");

                    b.Property<int>("RealmId")
                        .HasColumnType("int");

                    b.Property<int>("RegionId")
                        .HasColumnType("int");

                    b.Property<int>("ToonId")
                        .HasColumnType("int");

                    b.HasKey("PlayerId");

                    b.HasIndex("ToonId", "RegionId", "RealmId")
                        .IsUnique();

                    b.ToTable("Players");
                });

            modelBuilder.Entity("blizzardCrawler.db.MatchInfo", b =>
                {
                    b.HasOne("blizzardCrawler.db.Player", "Player")
                        .WithMany("MatchInfos")
                        .HasForeignKey("PlayerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Player");
                });

            modelBuilder.Entity("blizzardCrawler.db.Player", b =>
                {
                    b.Navigation("MatchInfos");
                });
#pragma warning restore 612, 618
        }
    }
}

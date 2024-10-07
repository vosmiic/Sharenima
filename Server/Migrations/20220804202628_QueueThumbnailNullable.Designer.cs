﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Sharenima.Server.Data;

#nullable disable

namespace Sharenima.Server.Migrations
{
    [DbContext(typeof(GeneralDbContext))]
    [Migration("20220804202628_QueueThumbnailNullable")]
    partial class QueueThumbnailNullable
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "6.0.6");

            modelBuilder.Entity("Sharenima.Shared.Instance", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<Guid>("CreateById")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Instances");
                });

            modelBuilder.Entity("Sharenima.Shared.Queue", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<Guid>("AddedById")
                        .HasColumnType("TEXT");

                    b.Property<Guid>("InstanceId")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Thumbnail")
                        .HasColumnType("TEXT");

                    b.Property<string>("Url")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("InstanceId");

                    b.ToTable("Queues");
                });

            modelBuilder.Entity("Sharenima.Shared.Queue", b =>
                {
                    b.HasOne("Sharenima.Shared.Instance", null)
                        .WithMany("VideoQueue")
                        .HasForeignKey("InstanceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Sharenima.Shared.Instance", b =>
                {
                    b.Navigation("VideoQueue");
                });
#pragma warning restore 612, 618
        }
    }
}
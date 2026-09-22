using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace TODOLISTAPI.Models;

public partial class TodoSmartAlertsContext : DbContext
{
    public TodoSmartAlertsContext()
    {
    }

    public TodoSmartAlertsContext(DbContextOptions<TodoSmartAlertsContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ForwardedTask> ForwardedTasks { get; set; }

    public virtual DbSet<GroupMember> GroupMembers { get; set; }

    public virtual DbSet<GroupsUser> GroupsUsers { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<SnoozeLog> SnoozeLogs { get; set; }

    public virtual DbSet<Task> Tasks { get; set; }

    public virtual DbSet<TaskMention> TaskMentions { get; set; }

    public virtual DbSet<TaskResponse> TaskResponses { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserLocation> UserLocations { get; set; }

    public virtual DbSet<UserSetting> UserSettings { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=RANAG\\SQLEXPRESS;Database=TodoSmartAlerts;Trusted_Connection=True;Encrypt=False;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ForwardedTask>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Forwarde__3214EC075A61EDB1");

            entity.Property(e => e.ForwardedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.ForwardedByNavigation).WithMany(p => p.ForwardedTaskForwardedByNavigations)
                .HasForeignKey(d => d.ForwardedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Forwarded_By");

            entity.HasOne(d => d.ForwardedToNavigation).WithMany(p => p.ForwardedTaskForwardedToNavigations)
                .HasForeignKey(d => d.ForwardedTo)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Forwarded_To");

            entity.HasOne(d => d.OriginalTask).WithMany(p => p.ForwardedTasks)
                .HasForeignKey(d => d.OriginalTaskId)
                .HasConstraintName("FK_Forwarded_Task");
        });

        modelBuilder.Entity<GroupMember>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__GroupMem__3214EC0784E9E556");

            entity.Property(e => e.AddedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Role)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("Member");

            entity.HasOne(d => d.Group).WithMany(p => p.GroupMembers)
                .HasForeignKey(d => d.GroupId)
                .HasConstraintName("FK_GroupMembers_Group");

            entity.HasOne(d => d.User).WithMany(p => p.GroupMembers)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_GroupMembers_User");
        });

        modelBuilder.Entity<GroupsUser>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__GroupsUs__3214EC0767F16719");

            entity.ToTable("GroupsUser");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .IsUnicode(false);

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.GroupsUsers)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GroupsUser_CreatedBy");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Notifica__3214EC07EB8250D3");

            entity.Property(e => e.Message).IsUnicode(false);
            entity.Property(e => e.SentAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Type)
                .HasMaxLength(30)
                .IsUnicode(false);

            entity.HasOne(d => d.Sender).WithMany(p => p.NotificationSenders)
                .HasForeignKey(d => d.SenderId)
                .HasConstraintName("FK_Notifications_Sender");

            entity.HasOne(d => d.Task).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.TaskId)
                .HasConstraintName("FK_Notifications_Task");

            entity.HasOne(d => d.User).WithMany(p => p.NotificationUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notifications_User");
        });

        modelBuilder.Entity<SnoozeLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__SnoozeLo__3214EC07D8C584B4");

            entity.Property(e => e.SnoozedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.WakeAt).HasColumnType("datetime");

            entity.HasOne(d => d.Task).WithMany(p => p.SnoozeLogs)
                .HasForeignKey(d => d.TaskId)
                .HasConstraintName("FK_Snooze_Task");

            entity.HasOne(d => d.User).WithMany(p => p.SnoozeLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Snooze_User");
        });

        modelBuilder.Entity<Task>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Tasks__3214EC07FA8CA435");

            entity.ToTable(tb => tb.HasTrigger("trg_Tasks_UpdatedAt"));

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Description).IsUnicode(false);
            entity.Property(e => e.GeofenceEnabled).HasDefaultValue(true);
            entity.Property(e => e.GeofenceRadiusMeters).HasDefaultValue(200);
            entity.Property(e => e.IsTimeBased).HasDefaultValue(true);
            entity.Property(e => e.Latitude).HasColumnType("decimal(10, 7)");
            entity.Property(e => e.Longitude).HasColumnType("decimal(10, 7)");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Pending");
            entity.Property(e => e.Title)
                .HasMaxLength(300)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.AssignedToNavigation).WithMany(p => p.TaskAssignedToNavigations)
                .HasForeignKey(d => d.AssignedTo)
                .HasConstraintName("FK_Tasks_AssignedTo");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.TaskCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Tasks_CreatedBy");

            entity.HasOne(d => d.Group).WithMany(p => p.Tasks)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Tasks_Group");
        });

        modelBuilder.Entity<TaskMention>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__TaskMent__3214EC0723C30B6C");

            entity.HasIndex(e => new { e.TaskId, e.MentionedUserId }, "UQ_TaskMention").IsUnique();

            entity.Property(e => e.MentionedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.MentionedByNavigation).WithMany(p => p.TaskMentionMentionedByNavigations)
                .HasForeignKey(d => d.MentionedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TaskMentions_MentionedBy");

            entity.HasOne(d => d.MentionedUser).WithMany(p => p.TaskMentionMentionedUsers)
                .HasForeignKey(d => d.MentionedUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TaskMentions_MentionedUser");

            entity.HasOne(d => d.Task).WithMany(p => p.TaskMentions)
                .HasForeignKey(d => d.TaskId)
                .HasConstraintName("FK_TaskMentions_Task");
        });

        modelBuilder.Entity<TaskResponse>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__TaskResp__3214EC0715247833");

            entity.HasIndex(e => new { e.TaskId, e.UserId }, "UQ_TaskResponses").IsUnique();

            entity.Property(e => e.RespondedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Response)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Task).WithMany(p => p.TaskResponses)
                .HasForeignKey(d => d.TaskId)
                .HasConstraintName("FK_TaskResponses_Task");

            entity.HasOne(d => d.User).WithMany(p => p.TaskResponses)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TaskResponses_User");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Users__3214EC070A53C32D");

            entity.HasIndex(e => e.Email, "UQ__Users__A9D10534AF9C5684").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.FirstName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LastLocationUpdatedAt).HasColumnType("datetime");
            entity.Property(e => e.LastName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<UserLocation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__UserLoca__3214EC0747FDD528");

            entity.HasIndex(e => e.UserId, "UQ__UserLoca__1788CC4D56EC433A").IsUnique();

            entity.Property(e => e.Accuracy).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Latitude).HasColumnType("decimal(10, 7)");
            entity.Property(e => e.Longitude).HasColumnType("decimal(10, 7)");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.User).WithOne(p => p.UserLocation)
                .HasForeignKey<UserLocation>(d => d.UserId)
                .HasConstraintName("FK_UserLocations_User");
        });

        modelBuilder.Entity<UserSetting>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__UserSett__3214EC070E164229");

            entity.HasIndex(e => e.UserId, "UQ__UserSett__1788CC4D7B6F48F8").IsUnique();

            entity.Property(e => e.NotificationEnabled).HasDefaultValue(true);
            entity.Property(e => e.SnoozeMinutes).HasDefaultValue(5);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.User).WithOne(p => p.UserSetting)
                .HasForeignKey<UserSetting>(d => d.UserId)
                .HasConstraintName("FK_UserSettings_User");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

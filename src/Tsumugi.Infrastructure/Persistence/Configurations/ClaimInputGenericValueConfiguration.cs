using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

/// <summary>
/// 汎用 pass-through 入力（ADR 0042）。親 <see cref="ClaimInput"/> の revision に属する子行で、
/// 独立した履歴を持たない（訂正は親の新 revision で集合を作り直す）。
/// </summary>
public sealed class ClaimInputGenericValueConfiguration : IEntityTypeConfiguration<ClaimInputGenericValue>
{
    public void Configure(EntityTypeBuilder<ClaimInputGenericValue> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ClaimInputGenericValues");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.ClaimInputId).IsRequired();
        builder.Property(value => value.Name).IsRequired().HasMaxLength(64);
        // 値は文字列 1 列。桁数は CSV 仕様側の宣言（maxBytes）で入力時に検証する。
        // 列長はどの項目でも収まる上限として置く（仕様の実値を DB へ持ち込まない）。
        builder.Property(value => value.Value).IsRequired().HasMaxLength(256);
        builder.HasIndex(value => new { value.ClaimInputId, value.Name }).IsUnique();
        // 親にナビゲーションを持たせない（明細行と同じ片方向 FK）。ナビゲーションを張ると EF の
        // relationship fix-up が不変コレクションへ子要素を追加しようとして読込が落ちる
        // （`ClaimInput.GenericValues` は record の値セマンティクスを保つため不変のままにする）。
        builder.HasOne<ClaimInput>()
            .WithMany()
            .HasForeignKey(value => value.ClaimInputId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

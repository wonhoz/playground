using StackCrash.Models;

namespace StackCrash.Levels;

public static class LevelData
{
    public static readonly IReadOnlyList<LevelDef> All = new[]
    {
        // ── Level 1: 단순 탑 ─────────────────────────────────────────
        new LevelDef("단순 탑", "기본 나무 탑을 무너뜨려라!", 5, 2, 4,
        [
            new(0f, 0.5f, 2.0f, 0.4f, 0f, BlockMaterial.Wood),   // 바닥 기둥
            new(0f, 1.2f, 1.6f, 0.4f, 0f, BlockMaterial.Wood),   // 2층
            new(0f, 1.9f, 1.2f, 0.4f, 0f, BlockMaterial.Wood),   // 3층
            new(0f, 2.6f, 0.8f, 0.4f, 0f, BlockMaterial.Wood),   // 4층
            new(0f, 3.3f, 0.4f, 0.4f, 0f, BlockMaterial.Wood),   // 꼭대기
        ]),

        // ── Level 2: 두 탑 ───────────────────────────────────────────
        new LevelDef("두 탑", "두 탑을 동시에 무너뜨려라!", 4, 2, 3,
        [
            // 왼쪽 탑
            new(-2.0f, 0.5f, 1.2f, 0.4f, 0f, BlockMaterial.Wood),
            new(-2.0f, 1.2f, 1.2f, 0.4f, 0f, BlockMaterial.Wood),
            new(-2.0f, 1.9f, 1.2f, 0.4f, 0f, BlockMaterial.Wood),
            // 오른쪽 탑
            new( 2.0f, 0.5f, 1.2f, 0.4f, 0f, BlockMaterial.Stone),
            new( 2.0f, 1.2f, 1.2f, 0.4f, 0f, BlockMaterial.Stone),
            new( 2.0f, 1.9f, 1.2f, 0.4f, 0f, BlockMaterial.Stone),
            // 연결 빔
            new( 0.0f, 2.5f, 4.4f, 0.3f, 0f, BlockMaterial.Wood),
        ]),

        // ── Level 3: 피라미드 ────────────────────────────────────────
        new LevelDef("피라미드", "돌 피라미드를 무너뜨려라!", 5, 2, 4,
        [
            new(-2.0f, 0.5f, 1.4f, 0.5f, 0f, BlockMaterial.Stone),
            new(-0.7f, 0.5f, 1.4f, 0.5f, 0f, BlockMaterial.Stone),
            new( 0.7f, 0.5f, 1.4f, 0.5f, 0f, BlockMaterial.Stone),
            new( 2.0f, 0.5f, 1.4f, 0.5f, 0f, BlockMaterial.Stone),
            new(-1.4f, 1.3f, 1.4f, 0.5f, 0f, BlockMaterial.Stone),
            new( 0.0f, 1.3f, 1.4f, 0.5f, 0f, BlockMaterial.Stone),
            new( 1.4f, 1.3f, 1.4f, 0.5f, 0f, BlockMaterial.Stone),
            new(-0.7f, 2.1f, 1.4f, 0.5f, 0f, BlockMaterial.Stone),
            new( 0.7f, 2.1f, 1.4f, 0.5f, 0f, BlockMaterial.Stone),
            new( 0.0f, 2.9f, 1.4f, 0.5f, 0f, BlockMaterial.Stone),
        ]),

        // ── Level 4: 얼음 탑 ─────────────────────────────────────────
        new LevelDef("얼음 탑", "미끄러운 얼음 탑을 처리하라!", 4, 1, 3,
        [
            new(-0.6f, 0.5f, 0.8f, 0.8f, 0f, BlockMaterial.Ice),
            new( 0.6f, 0.5f, 0.8f, 0.8f, 0f, BlockMaterial.Ice),
            new( 0.0f, 1.6f, 1.8f, 0.4f, 0f, BlockMaterial.Stone),
            new(-0.4f, 2.2f, 0.6f, 0.6f, 0f, BlockMaterial.Ice),
            new( 0.4f, 2.2f, 0.6f, 0.6f, 0f, BlockMaterial.Ice),
            new( 0.0f, 3.0f, 1.4f, 0.3f, 0f, BlockMaterial.Ice),
        ]),

        // ── Level 5: 폭발물 코어 ─────────────────────────────────────
        new LevelDef("폭발물 코어", "폭발물을 이용해 연쇄 폭발을 노려라!", 3, 1, 2,
        [
            new(-1.5f, 0.5f, 0.8f, 1.8f, 0f, BlockMaterial.Metal),
            new( 1.5f, 0.5f, 0.8f, 1.8f, 0f, BlockMaterial.Metal),
            new( 0.0f, 0.5f, 0.6f, 0.6f, 0f, BlockMaterial.Explosive),
            new( 0.0f, 1.4f, 2.6f, 0.4f, 0f, BlockMaterial.Wood),
            new(-0.8f, 2.0f, 0.6f, 0.6f, 0f, BlockMaterial.Explosive),
            new( 0.8f, 2.0f, 0.6f, 0.6f, 0f, BlockMaterial.Explosive),
            new( 0.0f, 2.7f, 1.8f, 0.4f, 0f, BlockMaterial.Wood),
            new( 0.0f, 3.2f, 0.6f, 0.6f, 0f, BlockMaterial.Explosive),
        ]),

        // ── Level 6: 돌+나무 혼합 ────────────────────────────────────
        new LevelDef("혼합 구조", "다양한 재질의 혼합 탑을 무너뜨려라!", 5, 2, 4,
        [
            new(-1.0f, 0.5f, 0.6f, 1.6f, 0f, BlockMaterial.Stone),
            new( 1.0f, 0.5f, 0.6f, 1.6f, 0f, BlockMaterial.Stone),
            new( 0.0f, 1.6f, 2.2f, 0.4f, 0f, BlockMaterial.Wood),
            new(-0.7f, 2.2f, 0.6f, 0.8f, 0f, BlockMaterial.Wood),
            new( 0.7f, 2.2f, 0.6f, 0.8f, 0f, BlockMaterial.Wood),
            new( 0.0f, 2.9f, 1.8f, 0.3f, 0f, BlockMaterial.Stone),
            new( 0.0f, 3.4f, 0.5f, 0.5f, 0f, BlockMaterial.Glass),
        ]),

        // ── Level 7: 기울어진 탑 ─────────────────────────────────────
        new LevelDef("기울어진 탑", "불안하게 쌓인 탑을 한 번에 쓰러뜨려라!", 3, 1, 2,
        [
            new( 0.0f, 0.5f,  2.0f, 0.4f,   0f, BlockMaterial.Stone),
            new( 0.2f, 1.2f,  1.8f, 0.4f,   5f, BlockMaterial.Wood),
            new( 0.5f, 1.9f,  1.6f, 0.4f,  10f, BlockMaterial.Wood),
            new( 0.9f, 2.6f,  1.4f, 0.4f,  15f, BlockMaterial.Wood),
            new( 1.4f, 3.3f,  1.2f, 0.4f,  20f, BlockMaterial.Wood),
            new( 2.0f, 4.0f,  0.8f, 0.4f,  25f, BlockMaterial.Wood),
        ]),

        // ── Level 8: 성채 ────────────────────────────────────────────
        new LevelDef("성채", "견고한 금속 성채를 폭파하라!", 6, 3, 5,
        [
            // 기초
            new(-2.5f, 0.5f, 0.8f, 2.0f, 0f, BlockMaterial.Metal),
            new( 2.5f, 0.5f, 0.8f, 2.0f, 0f, BlockMaterial.Metal),
            new( 0.0f, 0.3f, 4.2f, 0.4f, 0f, BlockMaterial.Stone),
            // 벽
            new(-1.6f, 1.2f, 0.6f, 1.4f, 0f, BlockMaterial.Stone),
            new( 1.6f, 1.2f, 0.6f, 1.4f, 0f, BlockMaterial.Stone),
            new( 0.0f, 1.2f, 0.6f, 1.4f, 0f, BlockMaterial.Stone),
            // 지붕 빔
            new( 0.0f, 2.2f, 3.6f, 0.4f, 0f, BlockMaterial.Metal),
            // 폭발물 숨김
            new(-0.8f, 1.2f, 0.5f, 0.5f, 0f, BlockMaterial.Explosive),
            new( 0.8f, 1.2f, 0.5f, 0.5f, 0f, BlockMaterial.Explosive),
            // 망루
            new(-2.5f, 2.8f, 0.8f, 0.8f, 0f, BlockMaterial.Metal),
            new( 2.5f, 2.8f, 0.8f, 0.8f, 0f, BlockMaterial.Metal),
            new( 0.0f, 3.0f, 1.0f, 0.6f, 0f, BlockMaterial.Glass),
        ]),
    };
}

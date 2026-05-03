using JeffopolyDeal.Models;

namespace JeffopolyDeal.ISMCTS
{
    // =========================================================================
    // BotPersonality.cs — Configurable personality profiles for bot AI
    // =========================================================================
    //
    // Each bot can have a different personality that affects how it scores
    // cards, evaluates board states, and makes strategic decisions. Personality
    // is assigned once when the bot is created and stays stable for the whole
    // game.
    //
    // Personality works by adjusting FEATURE-LEVEL parameters (thresholds,
    // base scores, multipliers) rather than just scaling final scores.
    // This produces meaningfully different play styles:
    //   - Aggressive bots attack more, defend less, play into JSN risk
    //   - Defensive bots build bank buffers, avoid exposed properties
    //   - Builders prioritize set completion over attacks
    //   - Chaotic bots explore more unusual plays via higher MCTS exploration
    //
    // IMPORTANT DESIGN DECISIONS:
    //   - Personality is separate from difficulty (ISMCTSConfig.Iterations).
    //     An aggressive bot can be easy (few iterations) or hard (many).
    //   - Balanced personality preserves the existing default behavior exactly.
    //   - During ISMCTS rollouts, the bot's personality is used for the bot's
    //     moves, and Balanced is assumed for all other players.
    // =========================================================================

    /// <summary>
    /// Defines a bot's strategic personality through feature-level parameters.
    /// Each parameter adjusts a specific aspect of decision-making rather than
    /// just scaling final scores.
    /// 
    /// <para><b>Game AI pattern:</b> This is a "parameterized policy" approach —
    /// the same decision logic is used by all bots, but the parameters that
    /// drive it differ. This is more maintainable than separate strategy
    /// classes and produces naturally varied play styles.</para>
    /// </summary>
    public class BotPersonality
    {
        // --- Attack parameters ---

        /// <summary>
        /// Base score for attack actions (Rent, Debt Collector, Birthday).
        /// Higher = more likely to play attacks over other options.
        /// Default: 1.0 (no modification). Range: 0.5 - 2.0.
        /// </summary>
        public double AttackWeight { get; set; } = 1.0;

        /// <summary>
        /// Base score for steal actions (Sly Deal, Forced Deal, Deal Breaker).
        /// Higher = more likely to steal properties vs build own sets.
        /// Default: 1.0. Range: 0.5 - 2.0.
        /// </summary>
        public double StealWeight { get; set; } = 1.0;

        // --- Defense parameters ---

        /// <summary>
        /// Target bank balance for rent buffer. The bot tries to maintain at
        /// least this much money before investing in non-completing properties.
        /// Higher = more defensive, keeps more cash reserves.
        /// Default: 5 (covers most single rent charges).
        /// </summary>
        public int RentBufferTarget { get; set; } = 5;

        /// <summary>
        /// How much to value bank money in board evaluation (HeuristicEval).
        /// Higher = bank is weighted more in ISMCTS position assessment.
        /// Default: 3.0 (per unit within rent buffer).
        /// </summary>
        public double BankValueWeight { get; set; } = 3.0;

        // --- Building parameters ---

        /// <summary>
        /// Bonus score for playing properties. Higher = prioritize building
        /// sets over banking money or attacking.
        /// Default: 1.0. Range: 0.5 - 2.0.
        /// </summary>
        public double PropertyWeight { get; set; } = 1.0;

        /// <summary>
        /// Bonus multiplier for near-complete sets. Higher = more aggressively
        /// pursue set completion.
        /// Default: 1.0. Range: 0.5 - 2.0.
        /// </summary>
        public double SetCompletionWeight { get; set; } = 1.0;

        // --- Risk parameters ---

        /// <summary>
        /// How much JSN probability affects attack decisions. 0.0 = ignore JSN
        /// risk entirely (yolo), 1.0 = fully discount attacks by JSN probability.
        /// Default: 0.5 (moderate caution).
        /// </summary>
        public double JsnRiskSensitivity { get; set; } = 0.5;

        // --- ISMCTS search parameters ---

        /// <summary>
        /// UCB1 exploration constant for ISMCTS. Higher = try more varied moves.
        /// Default: 1.0. Chaotic bots use 2.0+.
        /// </summary>
        public double ExplorationConstant { get; set; } = 1.0;

        // --- Targeting style ---

        /// <summary>
        /// How to choose attack targets. Affects Debt Collector, Sly Deal, etc.
        /// </summary>
        public TargetingStyle Targeting { get; set; } = TargetingStyle.BiggestThreat;

        // =====================================================================
        // Preset personalities
        // =====================================================================

        /// <summary>
        /// Default balanced play. Preserves existing behavior exactly.
        /// </summary>
        public static BotPersonality Balanced => new();

        /// <summary>
        /// Attacks aggressively with rent, steals, and debt. Keeps a thin
        /// bank and plays into JSN risk. Targets the biggest threat.
        /// </summary>
        public static BotPersonality Aggressive => new()
        {
            AttackWeight = 1.5,
            StealWeight = 1.4,
            RentBufferTarget = 3,
            BankValueWeight = 1.5,
            JsnRiskSensitivity = 0.2,
            Targeting = TargetingStyle.BiggestThreat,
        };

        /// <summary>
        /// Builds a large bank buffer and prioritizes properties that complete
        /// sets. Avoids risky attacks. Conservative play style.
        /// </summary>
        public static BotPersonality Defensive => new()
        {
            AttackWeight = 0.7,
            StealWeight = 0.8,
            RentBufferTarget = 8,
            BankValueWeight = 4.0,
            PropertyWeight = 1.2,
            SetCompletionWeight = 1.3,
            JsnRiskSensitivity = 0.8,
            Targeting = TargetingStyle.Weakest,
        };

        /// <summary>
        /// Focuses on completing property sets quickly. Plays properties and
        /// wildcards aggressively, uses steals to fill gaps.
        /// </summary>
        public static BotPersonality Builder => new()
        {
            AttackWeight = 0.6,
            StealWeight = 1.3,
            RentBufferTarget = 4,
            PropertyWeight = 1.5,
            SetCompletionWeight = 1.5,
            JsnRiskSensitivity = 0.4,
            Targeting = TargetingStyle.BiggestThreat,
        };

        /// <summary>
        /// High exploration, unpredictable play. Makes unusual moves that
        /// opponents can't easily predict. The wildcard bot.
        /// </summary>
        public static BotPersonality Chaotic => new()
        {
            AttackWeight = 1.3,
            StealWeight = 1.2,
            RentBufferTarget = 4,
            JsnRiskSensitivity = 0.1,
            ExplorationConstant = 2.0,
            Targeting = TargetingStyle.Random,
        };

        /// <summary>
        /// All available preset personalities for random assignment.
        /// </summary>
        public static BotPersonality[] AllPresets => new[]
        {
            Balanced, Aggressive, Defensive, Builder, Chaotic
        };

        /// <summary>
        /// Names corresponding to AllPresets, for display/logging.
        /// </summary>
        public static string[] PresetNames => new[]
        {
            "Balanced", "Aggressive", "Defensive", "Builder", "Chaotic"
        };

        /// <summary>
        /// Pick a random personality from the preset list.
        /// </summary>
        public static BotPersonality RandomPreset(Random rng)
        {
            var presets = AllPresets;
            return presets[rng.Next(presets.Length)];
        }

        /// <summary>
        /// Build an ISMCTSConfig from this personality's search parameters.
        /// Difficulty (iterations) is separate and passed in.
        /// </summary>
        public ISMCTSConfig ToISMCTSConfig(int iterations = 500, int timeLimitMs = 200)
        {
            return new ISMCTSConfig
            {
                Iterations = iterations,
                ExplorationConstant = ExplorationConstant,
                MaxRolloutTurns = 20,
                TimeLimitMs = timeLimitMs,
            };
        }
    }

    /// <summary>
    /// How the bot chooses targets for attack actions.
    /// </summary>
    public enum TargetingStyle
    {
        /// <summary>Target the player closest to winning (most complete sets, highest threat score).</summary>
        BiggestThreat,
        /// <summary>Target the player with the most total assets (bank + properties).</summary>
        Richest,
        /// <summary>Target the weakest player (easiest to extract value from).</summary>
        Weakest,
        /// <summary>Random targeting for unpredictable play.</summary>
        Random,
    }
}

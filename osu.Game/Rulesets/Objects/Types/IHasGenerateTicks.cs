// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Objects.Types
{
    /// <summary>
    /// A type of <see cref="HitObject"/> which explicitly specifies whether it should generate ticks.
    /// </summary>
    public interface IHasGenerateTicks
    {
        /// <summary>
        /// Whether or not slider ticks should be generated by this object.
        /// This exists for backwards compatibility with maps that abuse NaN slider velocity behavior on osu!stable (e.g. /b/2628991).
        /// </summary>
        bool GenerateTicks { get; set; }
    }
}

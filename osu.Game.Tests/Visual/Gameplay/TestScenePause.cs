// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Cursor;
using osu.Game.Rulesets;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Storyboards;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Gameplay
{
    public partial class TestScenePause : OsuPlayerTestScene
    {
        protected new PausePlayer Player => (PausePlayer)base.Player;

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard storyboard = null)
        {
            beatmap.AudioLeadIn = 4000;
            return base.CreateWorkingBeatmap(beatmap, storyboard);
        }

        private readonly Container content;

        protected override Container<Drawable> Content => content;

        private bool gameplayClockAlwaysGoingForward = true;
        private double lastForwardCheckTime;

        public TestScenePause()
        {
            base.Content.Add(content = new GlobalCursorDisplay { RelativeSizeAxes = Axes.Both });
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            LocalConfig.SetValue(OsuSetting.UIHoldActivationDelay, 0.0);
        }

        [SetUpSteps]
        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("resume player", () => Player.GameplayClockContainer.Start());
            confirmClockRunning(true);
        }

        [Test]
        public void TestTogglePauseViaBackAction()
        {
            pauseViaBackAction();
            pauseViaBackAction();
            confirmPausedWithNoOverlay();
            AddAssert("pause recorded", () => Player.Score.ScoreInfo.Pauses, () => Has.Count.EqualTo(1));
        }

        [Test]
        public void TestTogglePauseViaPauseGameplayAction()
        {
            pauseViaPauseGameplayAction();
            pauseViaPauseGameplayAction();
            confirmPausedWithNoOverlay();
            AddAssert("pause recorded", () => Player.Score.ScoreInfo.Pauses, () => Has.Count.EqualTo(1));
        }

        [Test]
        public void TestForwardPlaybackGuarantee()
        {
            hookForwardPlaybackCheck();

            AddUntilStep("wait for forward playback", () => Player.GameplayClockContainer.CurrentTime > 1000);
            AddStep("seek before gameplay", () => Player.GameplayClockContainer.Seek(-5000));

            checkForwardPlayback();
        }

        [Test]
        public void TestPauseWithLargeOffset()
        {
            AddStep("force large offset", () =>
            {
                var offset = (BindableDouble)LocalConfig.GetBindable<double>(OsuSetting.AudioOffset);

                // use a large negative offset to avoid triggering a fail from forwards seeking.
                offset.MinValue = -5000;
                offset.Value = -5000;
            });

            hookForwardPlaybackCheck();

            AddStep("move cursor outside", () => InputManager.MoveMouseTo(Player.ScreenSpaceDrawQuad.TopLeft - new Vector2(10)));

            pauseAndConfirm();

            resumeAndConfirm();

            checkForwardPlayback();

            AddStep("reset offset", () => LocalConfig.SetValue(OsuSetting.AudioOffset, 0.0));
        }

        private void checkForwardPlayback() => AddAssert("time didn't go too far backwards", () => gameplayClockAlwaysGoingForward);

        private void hookForwardPlaybackCheck()
        {
            AddStep("add time forward check hook", () =>
            {
                lastForwardCheckTime = double.MinValue;
                gameplayClockAlwaysGoingForward = true;

                Player.OnUpdate += _ =>
                {
                    var frameStableClock = Player.ChildrenOfType<FrameStabilityContainer>().Single().Clock;

                    double currentTime = frameStableClock.CurrentTime;

                    bool goingForward = currentTime >= lastForwardCheckTime;
                    lastForwardCheckTime = currentTime;

                    gameplayClockAlwaysGoingForward &= goingForward;

                    if (!goingForward)
                        Logger.Log($"Went too far backwards (last stop: {lastForwardCheckTime:N1} current: {currentTime:N1})");
                };
            });
        }

        [Test]
        public void TestPauseResume()
        {
            AddStep("move cursor outside", () => InputManager.MoveMouseTo(Player.ScreenSpaceDrawQuad.TopLeft - new Vector2(10)));
            pauseAndConfirm();
            AddAssert("player not playing", () => !Player.LocalUserPlaying.Value);

            resumeAndConfirm();

            AddAssert("continued playing forward", () => Player.LastResumeTime, () => Is.GreaterThanOrEqualTo(Player.LastPauseTime));

            AddUntilStep("player playing", () => Player.LocalUserPlaying.Value);
        }

        [Test]
        public void TestResumeWithResumeOverlay()
        {
            AddStep("move cursor to center", () => InputManager.MoveMouseTo(Player.ScreenSpaceDrawQuad.Centre));
            AddUntilStep("wait for hitobjects", () => Player.HealthProcessor.Health.Value < 1);

            pauseAndConfirm();
            resume();

            confirmPausedWithNoOverlay();
            AddStep("click to resume", () => InputManager.Click(MouseButton.Left));

            confirmClockRunning(true);
        }

        [Test]
        public void TestPauseWithResumeOverlay()
        {
            AddStep("move cursor to center", () => InputManager.MoveMouseTo(Player.ScreenSpaceDrawQuad.Centre));
            AddUntilStep("wait for hitobjects", () => Player.HealthProcessor.Health.Value < 1);

            pauseAndConfirm();
            resume();

            confirmPausedWithNoOverlay();
            pauseAndConfirm();

            AddUntilStep("resume overlay is not active", () => Player.DrawableRuleset.ResumeOverlay.State.Value == Visibility.Hidden);
            confirmPaused();
            confirmNotExited();
        }

        [Test]
        public void TestResumeWithResumeOverlaySkipped()
        {
            AddStep("move cursor to button", () =>
                InputManager.MoveMouseTo(Player.HUDOverlay.HoldToQuit.Children.OfType<HoldToConfirmContainer>().First().ScreenSpaceDrawQuad.Centre));
            AddUntilStep("wait for hitobjects", () => Player.HealthProcessor.Health.Value < 1);

            pauseAndConfirm();
            resumeAndConfirm();
        }

        [Test]
        public void TestUserPauseWhenPauseNotAllowed()
        {
            AddStep("disable pause support", () => Player.Configuration.AllowPause = false);

            pauseViaBackAction();
            confirmExited();
        }

        [Test]
        [Ignore("Fails on github runners if they happen to skip too far forward in time.")]
        public void TestUserPauseDuringCooldownTooSoon()
        {
            AddStep("seek to gameplay", () => Player.GameplayClockContainer.Seek(0));
            AddStep("move cursor outside", () => InputManager.MoveMouseTo(Player.ScreenSpaceDrawQuad.TopLeft - new Vector2(10)));

            pauseAndConfirm();

            resume();
            pauseViaBackAction();

            confirmResumed();
            confirmNotExited();
        }

        [Test]
        public void TestUserPauseDuringIntroSkipsCooldown()
        {
            AddStep("seek before gameplay", () => Player.GameplayClockContainer.Seek(-5000));
            AddStep("move cursor outside", () => InputManager.MoveMouseTo(Player.ScreenSpaceDrawQuad.TopLeft - new Vector2(10)));

            pauseAndConfirm();

            resume();
            pauseViaBackAction();
            confirmPaused();
        }

        [Test]
        public void TestQuickExitDuringCooldownTooSoon()
        {
            AddStep("seek to gameplay", () => Player.GameplayClockContainer.Seek(0));
            AddStep("move cursor outside", () => InputManager.MoveMouseTo(Player.ScreenSpaceDrawQuad.TopLeft - new Vector2(10)));

            pauseAndConfirm();

            resume();
            exitViaQuickExitAction();

            confirmResumed();
            AddAssert("exited", () => !Player.IsCurrentScreen());
        }

        [Test]
        public void TestExitSoonAfterResumeSucceeds()
        {
            AddStep("seek before gameplay", () => Player.GameplayClockContainer.Seek(-5000));

            pauseAndConfirm();
            resume();

            AddStep("exit quick", () => Player.Exit());

            confirmResumed();
            AddAssert("exited", () => !Player.IsCurrentScreen());
        }

        [Test]
        public void TestPauseAfterFail()
        {
            AddUntilStep("wait for fail", () => Player.GameplayState.HasFailed);
            AddUntilStep("fail overlay shown", () => Player.FailOverlayVisible);

            confirmClockRunning(false);

            AddStep("pause via forced pause", () => Player.Pause());

            confirmPausedWithNoOverlay();
            AddAssert("fail overlay still shown", () => Player.FailOverlayVisible);

            exitAndConfirm();
        }

        [Test]
        public void TestExitFromFailedGameplayAfterFailAnimation()
        {
            AddUntilStep("wait for fail", () => Player.GameplayState.HasFailed);
            AddUntilStep("wait for fail overlay shown", () => Player.FailOverlayVisible);

            confirmClockRunning(false);

            pauseViaBackAction();
            confirmExited();
        }

        [Test]
        public void TestExitFromFailedGameplayDuringFailAnimation()
        {
            AddUntilStep("wait for fail", () => Player.GameplayState.HasFailed);

            // will finish the fail animation and show the fail/pause screen.
            pauseViaBackAction();
            AddAssert("fail overlay shown", () => Player.FailOverlayVisible);

            // will actually exit.
            pauseViaBackAction();
            confirmExited();
        }

        [Test]
        public void TestQuickRetryFromFailedGameplay()
        {
            AddUntilStep("wait for fail", () => Player.GameplayState.HasFailed);
            AddStep("quick retry", () => Player.GameplayClockContainer.ChildrenOfType<HotkeyRetryOverlay>().First().Action?.Invoke());

            confirmExited();
        }

        [Test]
        public void TestQuickExitFromFailedGameplay()
        {
            AddUntilStep("wait for fail", () => Player.GameplayState.HasFailed);
            exitViaQuickExitAction();

            confirmExited();
        }

        [Test]
        public void TestExitFromGameplay()
        {
            // an externally triggered exit should immediately exit, skipping all pause logic.
            AddStep("exit", () => Player.Exit());
            confirmExited();
        }

        [Test]
        public void TestQuickExitFromGameplay()
        {
            exitViaQuickExitAction();

            confirmExited();
        }

        [Test]
        public void TestExitViaHoldToExit()
        {
            AddStep("set hold button always visible", () => LocalConfig.SetValue(OsuSetting.AlwaysShowHoldForMenuButton, true));

            AddStep("exit", () =>
            {
                InputManager.MoveMouseTo(Player.HUDOverlay.HoldToQuit.First(c => c is HoldToConfirmContainer));
                InputManager.PressButton(MouseButton.Left);
            });

            confirmPaused();

            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));

            exitAndConfirm();
        }

        [Test]
        public void TestExitFromPause()
        {
            pauseAndConfirm();
            exitAndConfirm();
        }

        [Test]
        public void TestRestartAfterResume()
        {
            AddStep("seek before gameplay", () => Player.GameplayClockContainer.Seek(-5000));

            pauseAndConfirm();
            resumeAndConfirm();
            restart();
            confirmExited();
        }

        [Test]
        public void TestPauseSoundLoop()
        {
            AddStep("seek before gameplay", () => Player.GameplayClockContainer.Seek(-5000));

            SkinnableSound getLoop() => Player.ChildrenOfType<PauseOverlay>().FirstOrDefault()?.ChildrenOfType<SkinnableSound>().FirstOrDefault();

            pauseAndConfirm();
            AddAssert("loop is playing", () => getLoop().IsPlaying);

            resumeAndConfirm();
            AddUntilStep("loop is stopped", () => !getLoop().IsPlaying);

            AddUntilStep("pause again", () =>
            {
                Player.Pause();
                return !Player.GameplayClockContainer.IsRunning;
            });

            AddAssert("loop is playing", () => getLoop().IsPlaying);

            resumeAndConfirm();
            AddUntilStep("loop is stopped", () => !getLoop().IsPlaying);
        }

        private void pauseAndConfirm()
        {
            pauseViaBackAction();
            confirmPaused();
        }

        private void resumeAndConfirm()
        {
            resume();
            confirmResumed();
        }

        private void exitAndConfirm()
        {
            confirmNotExited();
            AddStep("exit", () => Player.Exit());
            confirmExited();
            confirmNoTrackAdjustments();
        }

        private void confirmPaused()
        {
            confirmClockRunning(false);
            confirmNotExited();
            AddAssert("player not failed", () => !Player.GameplayState.HasFailed);
            AddAssert("pause overlay shown", () => Player.PauseOverlayVisible);
        }

        private void confirmResumed()
        {
            confirmClockRunning(true);
            confirmPauseOverlayShown(false);
        }

        private void confirmPausedWithNoOverlay()
        {
            confirmClockRunning(false);
            confirmPauseOverlayShown(false);
        }

        private void confirmExited() => AddUntilStep("player exited", () => !Player.IsCurrentScreen());
        private void confirmNotExited() => AddAssert("player not exited", () => Player.IsCurrentScreen());

        private void confirmNoTrackAdjustments()
        {
            AddUntilStep("track has no adjustments", () => Beatmap.Value.Track.AggregateFrequency.Value, () => Is.EqualTo(1));
        }

        private void restart() => AddStep("restart", () => Player.Restart());
        private void pauseViaBackAction() => AddStep("press escape", () => InputManager.Key(Key.Escape));
        private void pauseViaPauseGameplayAction() => AddStep("press middle mouse", () => InputManager.Click(MouseButton.Middle));

        private void exitViaQuickExitAction() => AddStep("press ctrl-tilde", () =>
        {
            InputManager.PressKey(Key.ControlLeft);
            InputManager.PressKey(Key.Tilde);
            InputManager.ReleaseKey(Key.Tilde);
            InputManager.ReleaseKey(Key.ControlLeft);
        });

        private void resume() => AddStep("resume", () => Player.Resume());

        private void confirmPauseOverlayShown(bool isShown) =>
            AddAssert("pause overlay " + (isShown ? "shown" : "hidden"), () => Player.PauseOverlayVisible == isShown);

        private void confirmClockRunning(bool isRunning) =>
            AddUntilStep("clock " + (isRunning ? "running" : "stopped"), () =>
            {
                bool completed = Player.GameplayClockContainer.IsRunning == isRunning;

                if (completed)
                {
                }

                return completed;
            });

        protected override bool AllowFail => true;

        protected override TestPlayer CreatePlayer(Ruleset ruleset) => new PausePlayer();

        protected partial class PausePlayer : TestPlayer
        {
            public double LastPauseTime { get; private set; }
            public double LastResumeTime { get; private set; }

            public bool FailOverlayVisible => FailOverlay.State.Value == Visibility.Visible;

            public bool PauseOverlayVisible => PauseOverlay.State.Value == Visibility.Visible;

            public override void OnEntering(ScreenTransitionEvent e)
            {
                base.OnEntering(e);
                GameplayClockContainer.Stop();
            }

            private bool? isRunning;

            protected override void UpdateAfterChildren()
            {
                base.UpdateAfterChildren();

                if (GameplayClockContainer.IsRunning != isRunning)
                {
                    isRunning = GameplayClockContainer.IsRunning;

                    if (isRunning.Value)
                        LastResumeTime = GameplayClockContainer.CurrentTime;
                    else
                        LastPauseTime = GameplayClockContainer.CurrentTime;
                }
            }
        }
    }
}

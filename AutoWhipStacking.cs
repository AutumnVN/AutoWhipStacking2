using System;
using Terraria;
using Terraria.ID;
using TerrariaModder.Core;
using TerrariaModder.Core.Config;
using TerrariaModder.Core.Events;
using TerrariaModder.Core.Logging;

namespace AutoWhipStacking
{
    public class AutoWhipStackingConfig : ModConfig
    {
        public override int Version => 1;

        [Client, Label("Enabled"), Description("Automatically switch between whips while attacking.")]
        public bool Enabled { get; set; } = true;

        [Client, Label("Whip Loop Delay"), Description("Delay in seconds before looping back to the first whip."), Range(3, 12)]
        public int WhipLoopDelay { get; set; } = 8;
    }

    public class Mod : IMod, IModLifecycle
    {
        public string Id => "auto-whip-stacking";
        public string Name => "Auto Whip Stacking";
        public string Version => "1.0.0";

        private ILogger _log;
        private AutoWhipStackingConfig _config;

        private bool _hitLandedThisSwing;
        private int _lastAnimation;
        private uint _lastTimeSecondWhipHit;

        public void Initialize(ModContext context)
        {
            _log = context.Logger;
            _config = context.GetConfig<AutoWhipStackingConfig>();

            FrameEvents.OnPostUpdate += OnPostUpdate;
            GameEvents.OnWorldLoad += OnWorldLoad;
            GameEvents.OnWorldUnload += OnWorldUnload;

            _log.Info($"{Name} v{Version} initialized");
        }

        public void OnContentReady(ModContext context)
        {
        }

        public void OnConfigChanged()
        {
            _log.Info($"Config changed: Enabled={_config.Enabled}, WhipLoopDelay={_config.WhipLoopDelay}");
        }

        public void OnWorldLoad()
        {
            ResetRuntimeState();
        }

        public void OnWorldUnload()
        {
            ResetRuntimeState();
        }

        public void Unload()
        {
            FrameEvents.OnPostUpdate -= OnPostUpdate;
            GameEvents.OnWorldLoad -= OnWorldLoad;
            GameEvents.OnWorldUnload -= OnWorldUnload;
            ResetRuntimeState();

            _log.Info($"{Name} unloaded");
        }

        private void OnPostUpdate()
        {
            if (!IsEnabled()) return;

            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead || player.whoAmI != Main.myPlayer) return;

            int currentAnim = player.itemAnimation;

            if (_lastAnimation == 0 && currentAnim > 0)
            {
                _hitLandedThisSwing = false;
            }

            if (currentAnim > 0 && !_hitLandedThisSwing)
            {
                CheckWhipHits(player);
            }

            _lastAnimation = currentAnim;
        }

        private void CheckWhipHits(Player player)
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == player.whoAmI && ProjectileID.Sets.IsAWhip[proj.type])
                {
                    if (proj.numHits > 0)
                    {
                        _hitLandedThisSwing = true;
                        if (player.selectedItem == GetSecondWhipSlot(player))
                        {
                            _lastTimeSecondWhipHit = Main.GameUpdateCount;
                        }
                        TrySwitchWhip(player);
                        return;
                    }
                }
            }
        }

        private void TrySwitchWhip(Player player)
        {
            int firstWhipSlot = GetFirstWhipSlot(player);
            if (firstWhipSlot < 0) return;

            int currentSlot = player.selectedItem;

            long ticksSinceSecondWhip = (long)Main.GameUpdateCount - _lastTimeSecondWhipHit + player.HeldItem.useTime;
            long delayTicks = 60L * _config.WhipLoopDelay;

            bool shouldSwitch =
                (currentSlot == firstWhipSlot && ticksSinceSecondWhip > delayTicks) ||
                currentSlot != firstWhipSlot;

            if (shouldSwitch)
            {
                SwitchToNextWhip(player);
            }
        }

        private void SwitchToNextWhip(Player player)
        {
            for (int i = 1; i < 10; i++)
            {
                int nextSlot = (player.selectedItem + i) % 10;
                if (IsWhipItem(player.inventory[nextSlot]))
                {
                    player.changeItem = nextSlot;
                    return;
                }
            }
        }

        private int GetFirstWhipSlot(Player player)
        {
            for (int i = 0; i < 10; i++)
            {
                if (IsWhipItem(player.inventory[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private int GetSecondWhipSlot(Player player)
        {
            int firstWhipSlot = GetFirstWhipSlot(player);
            for (int i = 0; i < 10; i++)
            {
                if (i != firstWhipSlot && IsWhipItem(player.inventory[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private bool IsWhipItem(Item item)
        {
            return item != null && !item.IsAir && item.shoot > 0 && ProjectileID.Sets.IsAWhip[item.shoot];
        }

        private bool IsEnabled()
        {
            return _config == null || _config.Enabled;
        }

        private void ResetRuntimeState()
        {
            _hitLandedThisSwing = false;
            _lastAnimation = 0;
            _lastTimeSecondWhipHit = 0;
        }
    }
}

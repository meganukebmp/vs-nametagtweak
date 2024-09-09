using Vintagestory.API.Client;
using Vintagestory.API.Common;
using HarmonyLib;
using Vintagestory.GameContent;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Collections.Generic;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace NameTagTweak
{
    public class NameTagTweak : ModSystem
    {
        public const string harmonynamespace = "bg.dragon.nametagtweak";
        private Harmony harmony = new Harmony(harmonynamespace);

        // Config structure
        public class NameTagConfig
        {
            public double[] BackgroundColor;
            public double[] TextColor;
            public int TextPadding;
            public double BorderRadius;
            public bool DropShadow;
            public double BorderWidth;
            public double[] BorderColor;
        }
        public const string configfilename = "nametagtweak.json";

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Logger.Notification("NameTagTweak Loaded!");

            NameTagConfig currentConfig;

            try
            {
                currentConfig = api.LoadModConfig<NameTagConfig>(configfilename);
                if (currentConfig == null)
                {
                    // File doesnt exist
                    throw new Exception("File doesnt exist!");
                }

            }
            catch
            {
                api.Logger.Error("Did not find nametagtweak config file! Using default...");
                // Failed to load config, use default config
                currentConfig = api.Assets.Get<NameTagConfig>(new AssetLocation("nametagtweak", "config/" + configfilename));
                // Store the config
                api.StoreModConfig<NameTagConfig>(currentConfig, configfilename);
            }

            PatchNametag.config = currentConfig;

            // Patch the nametag renderer
            MethodInfo targetMethod = AccessTools.Method(typeof(EntityNameTagRendererRegistry), "GetNameTagRenderer");
            MethodInfo prefixMethod = typeof(PatchNametag).GetMethod(nameof(PatchNametag.Prefix));
            harmony.Patch(targetMethod, new HarmonyMethod(prefixMethod));
        }

        public override void Dispose()
        {
            // Unpatch
            harmony.UnpatchAll(harmonynamespace);
        }

        public static class PatchNametag
        {
            public static NameTagConfig config;
            public static LoadedTexture DefaultTexture(ICoreClientAPI capi, Entity entity)
            {
                string displayName = entity.GetBehavior<EntityBehaviorNameTag>()?.DisplayName;
                if (displayName == null || displayName.Length <= 0)
                {
                    return (LoadedTexture)null;
                }

                return capi.Gui.TextTexture.GenUnscaledTextTexture(displayName, CairoFont.WhiteMediumText().WithColor(config.TextColor), new TextBackground()
                {
                    FillColor = config.BackgroundColor,
                    Padding = config.TextPadding,
                    Radius = config.BorderRadius,
                    Shade = config.DropShadow,
                    BorderColor = config.BorderColor,
                    BorderWidth = config.BorderWidth
                });
            }

            public static bool Prefix(EntityNameTagRendererRegistry __instance, ref NameTagRendererDelegate __result, ref Entity entity)
            {
                // Get a list of entitlements (devs and other people have special nametags, keep those as is)
                List<Entitlement> entitlements = entity is EntityPlayer entityPlayer ? entityPlayer.Player?.Entitlements : (List<Entitlement>)null;

                if (entitlements != null && entitlements?.Count > 0)
                {
                    Entitlement entitlement = entitlements[0];
                    double[] numArray = (double[])null;
                    if (GlobalConstants.playerColorByEntitlement.TryGetValue(entitlement.Code, out numArray))
                    {
                        TextBackground textBackground;
                        GlobalConstants.playerTagBackgroundByEntitlement.TryGetValue(entitlement.Code, out textBackground);
                        __result = new NameTagRendererDelegate(new EntityNameTagRendererRegistry.DefaultEntitlementTagRenderer()
                        {
                            color = numArray,
                            background = textBackground
                        }.renderTag);
                        return false;
                    }
                }

                // Patch only default nametags
                __result = new NameTagRendererDelegate(DefaultTexture);
                return false;
            }
        }
    }
}

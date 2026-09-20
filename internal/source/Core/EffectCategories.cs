using System;
using System.Collections.Generic;
using System.Linq;

namespace murumsWiiModStudio
{
    internal sealed class EffectCategory
    {
        internal readonly string Id, Label;
        internal readonly string[] Prefixes;
        internal EffectCategory(string id, string german, string english, params string[] prefixes)
        {
            Id = id;
            Label = L.T(german, english);
            Prefixes = prefixes;
        }
        public override string ToString() { return Label; }
    }

    internal static class EffectCategories
    {
        internal static EffectCategory[] Create()
        {
            return new[]
            {
                new EffectCategory("all", "Alle Effekte", "All effects"),
                new EffectCategory("drift-blue", "Drift: blaue Funken", "Drift: blue sparks", "rk_blueSpark", "rk_blueChip", "rk_driftSpark1"),
                new EffectCategory("drift-orange", "Drift: orange Funken", "Drift: orange sparks", "rk_orangeSpark", "rk_orangeChip", "rk_driftSpark2"),
                new EffectCategory("drift-purple", "Drift: violette Funken (RR)", "Drift: purple sparks (RR)", "rk_purpleSpark", "rk_purpleChip", "rk_driftSpark3"),
                new EffectCategory("star", "Stern: Unverwundbarkeit", "Star: invincibility"),
                new EffectCategory("boost", "Turbo / Boost: Standard", "Turbo / boost: standard", "rk_miniTurbo", "rk_boost", "rk_dash"),
                new EffectCategory("lightning", "Item: Blitz", "Item: lightning", "rk_thunder"),
                new EffectCategory("slipstream-charge", "Windschatten: Aufbau", "Slipstream: charging", "rk_slipStreamPre"),
                new EffectCategory("slipstream-boost", "Windschatten: Boost", "Slipstream: boost", "rk_slipStream"),
                new EffectCategory("boost-orange", "Turbo: Orange (RR)", "Turbo: orange (RR)", "rk_orangeTurbo"),
                new EffectCategory("boost-purple", "Turbo: Violett (RR)", "Turbo: purple (RR)", "rk_purpleTurbo"),
                new EffectCategory("trick", "Tricks: Glanz / Sterne", "Tricks: glow / stars", "rk_jumpSeikou"),
                new EffectCategory("wheelie", "Wheelie", "Wheelie", "rk_wheelie"),
                new EffectCategory("start-success", "Start: erfolgreicher Boost", "Start: successful boost", "rk_startSeikou"),
                new EffectCategory("start-fail", "Start: Fehlstart", "Start: burnout", "rk_startMiss"),
                new EffectCategory("start-charge", "Start: Aufladen / Reifen", "Start: charging / wheels", "rk_start", "rk_wheelSpin"),
                new EffectCategory("drift-smoke", "Drift: Rauch / Automatik", "Drift: smoke / automatic", "rk_driftSmoke", "rk_autoDrift"),
                new EffectCategory("exhaust", "Auspuffrauch", "Exhaust smoke", "rk_gasSmoke"),
                new EffectCategory("brakes", "Bremsen", "Braking", "rk_brake"),
                new EffectCategory("jump", "Sprung / Landung", "Jumping / landing", "rk_jumpSmoke", "rk_jumpTail"),
                new EffectCategory("speed", "Fahrtwind / Bewegungslinien", "Speed / motion trails", "rk_koukasen", "rk_hangOn"),
                new EffectCategory("hit-stars", "Treffer: Sterne", "Hits: stars", "rk_ac_star"),
                new EffectCategory("collision", "Treffer / Zusammenstoß", "Hits / collisions", "rk_crash", "rk_hit", "rk_ac_fireChp", "rk_ac_hitMrk", "rk_uchiage", "rk_kurokoge"),
                new EffectCategory("spin", "Dreher / Schleudern", "Spinning / skidding", "rk_spin"),
                new EffectCategory("shell", "Item: Panzer", "Item: shells", "rk_koura", "rk_haneKoura"),
                new EffectCategory("bomb", "Item: Explosionen", "Item: explosions", "rk_bomb"),
                new EffectCategory("blooper", "Item: Blooper-Tinte", "Item: Blooper ink", "rk_gesso"),
                new EffectCategory("mega", "Item: Mega-Pilz", "Item: Mega Mushroom", "rk_kyodai"),
                new EffectCategory("pow", "Item: POW", "Item: POW", "rk_pow"),
                new EffectCategory("cloud", "Item: Wolkenblitz", "Item: Thunder Cloud", "rk_kaminariGumo"),
                new EffectCategory("banana", "Item: Banane", "Item: banana", "rk_banana"),
                new EffectCategory("bullet", "Item: Kugelwilli", "Item: Bullet Bill", "rk_killer"),
                new EffectCategory("fake-box", "Item: falsche Item-Box", "Item: Fake Item Box", "rk_niseBox"),
                new EffectCategory("item-box", "Item-Box / Einsammeln", "Item Box / collection", "rk_itemBox", "rk_itemVan"),
                new EffectCategory("oil", "Untergrund: Öl", "Surface: oil", "rk_oil"),
                new EffectCategory("grass", "Untergrund: Gras / Blumen", "Surface: grass / flowers", "rk_weed", "rk_flower"),
                new EffectCategory("sand", "Untergrund: Sand / Staub", "Surface: sand / dust", "rk_dirt"),
                new EffectCategory("mud", "Untergrund: Schlamm", "Surface: mud", "rk_mud"),
                new EffectCategory("stone", "Untergrund: Steine", "Surface: stones", "rk_stone"),
                new EffectCategory("water", "Untergrund: Wasser", "Surface: water", "rk_water", "rk_pochaWater", "rk_wave", "rk_awa", "rk_jugemWater"),
                new EffectCategory("snow", "Untergrund: Schnee", "Surface: snow", "rk_snow", "rk_deepSnow"),
                new EffectCategory("ice", "Untergrund: Eis", "Surface: ice", "rk_ice"),
                new EffectCategory("ambient", "Umgebung: Nebel / Sporen", "Environment: mist / spores", "rk_moya", "rk_housi", "rk_kinokoCloud"),
                new EffectCategory("chain-chomp", "Umgebung: Kettenhund", "Environment: Chain Chomp", "rk_wanSmk"),
                new EffectCategory("balloon", "Kampf: Ballons", "Battle: balloons", "rk_baloon"),
                new EffectCategory("other", "Weitere Effekte", "Other effects")
            };
        }

        internal static string Classify(string name, IEnumerable<EffectCategory> categories)
        {
            name = name ?? "";
            if (new[] { "rk_star", "rk_starS", "rk_star0_draw", "rk_star1_spin" }.Contains(name, StringComparer.OrdinalIgnoreCase))
                return "star";
            foreach (var category in categories)
                if (category.Prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                    return category.Id;
            return "other";
        }
    }
}

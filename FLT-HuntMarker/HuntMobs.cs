using System.Collections.Generic;

namespace FLT_HuntMarker
{
    // Elite marks by BNpcName row id (ActorItem.NPCID2 = Character.NameId), same in every client language.
    // IDs from Faloop mob data (faloop.app main.js, via jshade2710/faloop-messenger)
    static class HuntMobs
    {
        // BNpcName id => rank ("s" / "a" / "b")
        public static readonly Dictionary<uint, string> Ranks = new()
        {
            // ---- S rank (SS included) ----
            // A Realm Reborn
            { 2953, "s" }, // Laideronnette
            { 2954, "s" }, // Wulgaru
            { 2955, "s" }, // Mindflayer
            { 2956, "s" }, // Thousand-cast Theda
            { 2957, "s" }, // Zona Seeker
            { 2958, "s" }, // Brontes
            { 2959, "s" }, // Lampalagua
            { 2960, "s" }, // Nunyunuwi
            { 2961, "s" }, // Minhocao
            { 2962, "s" }, // Croque-mitaine
            { 2963, "s" }, // Croakadile
            { 2964, "s" }, // The Garlok
            { 2965, "s" }, // Bonnacon
            { 2966, "s" }, // Nandi
            { 2967, "s" }, // Chernobog
            { 2968, "s" }, // Safat
            { 2969, "s" }, // Agrippa the Mighty
            // Heavensward
            { 4374, "s" }, // Kaiser Behemoth
            { 4375, "s" }, // Senmurv
            { 4376, "s" }, // The Pale Rider
            { 4377, "s" }, // Gandarewa
            { 4378, "s" }, // Bird of Paradise
            { 4380, "s" }, // Leucrotta
            // Stormblood
            { 5984, "s" }, // Okina
            { 5985, "s" }, // Gamma
            { 5986, "s" }, // Orghana
            { 5987, "s" }, // Udumbara
            { 5988, "s" }, // Bone Crawler
            { 5989, "s" }, // Salt and Light
            // Shadowbringers
            { 8653, "s" }, // Aglaope
            { 8890, "s" }, // Ixtab
            { 8895, "s" }, // Gunitt
            { 8900, "s" }, // Tarchia
            { 8905, "s" }, // Tyger
            { 8910, "s" }, // Forgiven Pedantry
            { 8915, "s" }, // Forgiven Rebellion (SS)
            // Endwalker
            { 10615, "s" }, // Ker (SS)
            { 10617, "s" }, // Burfurlur the Canny
            { 10618, "s" }, // Sphatika
            { 10619, "s" }, // Armstrong
            { 10620, "s" }, // Ruminator
            { 10621, "s" }, // Ophioneus
            { 10622, "s" }, // Narrow-rift
            // Dawntrail
            { 12754, "s" }, // Neyoozoteel
            { 13156, "s" }, // Atticus the Primogenitor
            { 13360, "s" }, // Kirlirger the Abhorrent
            { 13399, "s" }, // Sansheya
            { 13406, "s" }, // Arch Aethereater (SS)
            { 13437, "s" }, // The Forecaster
            { 13444, "s" }, // Ihnuxokiy

            // ---- A rank ----
            // A Realm Reborn
            { 2936, "a" }, // Forneus
            { 2937, "a" }, // Melt
            { 2938, "a" }, // Ghede Ti Malice
            { 2939, "a" }, // Girtab
            { 2940, "a" }, // Alectryon
            { 2941, "a" }, // Sabotender Bailarina
            { 2942, "a" }, // Maahes
            { 2943, "a" }, // Zanig'oh
            { 2944, "a" }, // Dalvag's Final Flame
            { 2945, "a" }, // Vogaal Ja
            { 2946, "a" }, // Unktehi
            { 2947, "a" }, // Hellsclaw
            { 2948, "a" }, // Nahn
            { 2949, "a" }, // Marberry
            { 2950, "a" }, // Cornu
            { 2951, "a" }, // Marraco
            { 2952, "a" }, // Kurrea
            // Heavensward
            { 4362, "a" }, // Mirka
            { 4363, "a" }, // Lyuba
            { 4364, "a" }, // Pylraster
            { 4365, "a" }, // Lord of the Wyverns
            { 4366, "a" }, // Slipkinx Steeljoints
            { 4367, "a" }, // Stolas
            { 4368, "a" }, // Bune
            { 4369, "a" }, // Agathos
            { 4370, "a" }, // Enkelados
            { 4371, "a" }, // Sisiutl
            { 4372, "a" }, // Campacti
            { 4373, "a" }, // Stench Blossom
            // Stormblood
            { 5990, "a" }, // Orcus
            { 5991, "a" }, // Erle
            { 5992, "a" }, // Vochstein
            { 5993, "a" }, // Aqrabuamelu
            { 5994, "a" }, // Mahisha
            { 5995, "a" }, // Luminare
            { 5996, "a" }, // Funa Yurei
            { 5997, "a" }, // Oni Yumemi
            { 5998, "a" }, // Gajasura
            { 5999, "a" }, // Angada
            { 6000, "a" }, // Girimekhala
            { 6001, "a" }, // Sum
            // Shadowbringers
            { 8654, "a" }, // The Mudman
            { 8655, "a" }, // O Poorest Pauldia
            { 8891, "a" }, // Supay
            { 8892, "a" }, // Grassman
            { 8896, "a" }, // Rusalka
            { 8897, "a" }, // Baal
            { 8901, "a" }, // Maliktender
            { 8902, "a" }, // Sugaar
            { 8906, "a" }, // Nuckelavee
            { 8907, "a" }, // Nariphon
            { 8911, "a" }, // Li'l Murderer
            { 8912, "a" }, // Huracan
            // Endwalker
            { 10623, "a" }, // Storsie
            { 10624, "a" }, // Hulder
            { 10625, "a" }, // Yilan
            { 10626, "a" }, // Sugriva
            { 10627, "a" }, // Minerva
            { 10628, "a" }, // Aegeiros
            { 10629, "a" }, // Lunatender Queen
            { 10630, "a" }, // Mousse Princess
            { 10631, "a" }, // Gurangatch
            { 10632, "a" }, // Petalodus
            { 10633, "a" }, // Fan Ail
            { 10634, "a" }, // Arch-Eta
            // Dawntrail
            { 12692, "a" }, // Starcrier
            { 12753, "a" }, // Rrax Yity'a
            { 13157, "a" }, // Heshuala
            { 13158, "a" }, // Urna Variabilis
            { 13361, "a" }, // Queen Hawk
            { 13362, "a" }, // Nechuciho
            { 13400, "a" }, // Yehehetoaua'pyo
            { 13401, "a" }, // Keheniheyamewi
            { 13435, "a" }, // Sally the Sweeper
            { 13436, "a" }, // Cat's Eye
            { 13442, "a" }, // The Raintriller
            { 13443, "a" }, // Pkuucha

            // ---- B rank ----
            // A Realm Reborn
            { 2919, "b" }, // White Joker
            { 2920, "b" }, // Stinging Sophie
            { 2921, "b" }, // Monarch Ogrefly
            { 2922, "b" }, // Phecda
            { 2923, "b" }, // Sewer Syrup
            { 2924, "b" }, // Ovjang
            { 2925, "b" }, // Gatling
            { 2926, "b" }, // Albin the Ashen
            { 2927, "b" }, // Flame Sergeant Dalvag
            { 2928, "b" }, // Skogs Fru
            { 2929, "b" }, // Barbastelle
            { 2930, "b" }, // Bloody Mary
            { 2931, "b" }, // Dark Helmet
            { 2932, "b" }, // Myradrosh
            { 2933, "b" }, // Vuokho
            { 2934, "b" }, // Naul
            { 2935, "b" }, // Leech King
            // Heavensward
            { 4350, "b" }, // Alteci
            { 4351, "b" }, // Kreutzet
            { 4352, "b" }, // Gnath Cometdrone
            { 4353, "b" }, // Thextera
            { 4354, "b" }, // Pterygotus
            { 4355, "b" }, // False Gigantopithecus
            { 4356, "b" }, // Scitalis
            { 4357, "b" }, // The Scarecrow
            { 4358, "b" }, // Squonk
            { 4359, "b" }, // Sanu Vali of Dancing Wings
            { 4360, "b" }, // Lycidas
            { 4361, "b" }, // Omni
            // Stormblood
            { 6002, "b" }, // Gauki Strongblade
            { 6003, "b" }, // Guhuo Niao
            { 6004, "b" }, // Deidar
            { 6005, "b" }, // Gyorai Quickstrike
            { 6006, "b" }, // Kurma
            { 6007, "b" }, // Aswang
            { 6008, "b" }, // Shadow-dweller Yamini
            { 6009, "b" }, // Ouzelum
            { 6010, "b" }, // Gwas-y-neidr
            { 6011, "b" }, // Buccaboo
            { 6012, "b" }, // Manes
            { 6013, "b" }, // Kiwa
            // Shadowbringers
            { 8656, "b" }, // Domovoi
            { 8657, "b" }, // Vulpangue
            { 8893, "b" }, // Mindmaker
            { 8894, "b" }, // Pachamama
            { 8898, "b" }, // Gilshs Aath Swiftclaw
            { 8899, "b" }, // Deacon
            { 8903, "b" }, // Worm of the Well
            { 8904, "b" }, // Juggler Hecatomb
            { 8908, "b" }, // La Velue
            { 8909, "b" }, // Itzpapalotl
            { 8913, "b" }, // Coquecigrue
            { 8914, "b" }, // Indomitable
            // Endwalker
            { 10635, "b" }, // Green Archon
            { 10636, "b" }, // Ü-u-ü-u
            { 10637, "b" }, // Vajrakumara
            { 10638, "b" }, // Iravati
            { 10639, "b" }, // Warmonger
            { 10640, "b" }, // Emperor's Rose
            { 10641, "b" }, // Daphnia Magna
            { 10642, "b" }, // Genesis Rock
            { 10643, "b" }, // Yumcax
            { 10644, "b" }, // Shockmaw
            { 10645, "b" }, // Level Cheater
            { 10646, "b" }, // Oskh Rhei
            // Dawntrail
            { 13144, "b" }, // Mad Maguey
            { 13145, "b" }, // Chupacabra
            { 13146, "b" }, // The Slammer
            { 13147, "b" }, // Go'ozoabek'be
            { 13148, "b" }, // Leafscourge Hadoll Ja
            { 13149, "b" }, // Xty'iinbek
            { 13150, "b" }, // Nopalitender Fabuloso
            { 13151, "b" }, // Uktena
            { 13152, "b" }, // Gallowsbeak
            { 13153, "b" }, // Gargant
            { 13154, "b" }, // Jewel Bearer
            { 13155, "b" }, // 13th Child
        };
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Globalization;
using osu.Framework.Configuration;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Users;

namespace osu.Game.Extensions
{
    /// <summary>
    /// Conversion utilities for the <see cref="Language"/> enum.
    /// </summary>
    public static class LanguageExtensions
    {
        /// <summary>
        /// Returns the culture code of the <see cref="CultureInfo"/> that corresponds to the supplied <paramref name="language"/>.
        /// </summary>
        /// <remarks>
        /// This is required as enum member names are not allowed to contain hyphens.
        /// </remarks>
        public static string ToCultureCode(this Language language)
        {
            if (language == Language.zh_hant)
                return @"zh-tw";

            return language.ToString().Replace("_", "-");
        }

        /// <summary>
        /// Attempts to parse the supplied <paramref name="cultureCode"/> to a <see cref="Language"/> value.
        /// </summary>
        /// <param name="cultureCode">The code of the culture to parse.</param>
        /// <param name="language">The parsed <see cref="Language"/>. Valid only if the return value of the method is <see langword="true" />.</param>
        /// <returns>Whether the parsing succeeded.</returns>
        public static bool TryParseCultureCode(string cultureCode, out Language language)
        {
            if (cultureCode == @"zh-tw")
            {
                language = Language.zh_hant;
                return true;
            }

            return Enum.TryParse(cultureCode.Replace("-", "_"), out language);
        }

        /// <summary>
        /// Parses the <see cref="Language"/> that is specified in <paramref name="frameworkLocale"/>,
        /// or if that is not valid, the language of the current <see cref="ResourceManagerLocalisationStore"/> as exposed by <paramref name="localisationParameters"/>.
        /// </summary>
        /// <param name="frameworkLocale">The current <see cref="FrameworkSetting.Locale"/>.</param>
        /// <param name="localisationParameters">The current <see cref="LocalisationParameters"/> of the <see cref="LocalisationManager"/>.</param>
        /// <returns>The parsed language.</returns>
        public static Language GetLanguageFor(string frameworkLocale, LocalisationParameters localisationParameters)
        {
            // the usual case when the user has changed the language
            if (TryParseCultureCode(frameworkLocale, out var language))
                return language;

            if (localisationParameters.Store != null)
            {
                // startup case, locale not explicitly set, or the set language was removed in an update
                if (TryParseCultureCode(localisationParameters.Store.EffectiveCulture.Name, out language))
                    return language;
            }

            return Language.en;
        }

        /// <summary>
        /// Convert the <see cref="Language"/> to the corresponding <see cref="CountryCode"/>.
        /// </summary>
        /// <param name="language">The <see cref="Language"/>.</param>
        /// <returns>The corresponding <see cref="CountryCode"/>.</returns>
        public static CountryCode GetCountryCode(Language language) => language switch
        {
            Language.en => CountryCode.GB,
            Language.be => CountryCode.BY,
            Language.bg => CountryCode.BG,
            Language.ca => CountryCode.AD,
            Language.cs => CountryCode.CZ,
            Language.da => CountryCode.DK,
            Language.de => CountryCode.DE,
            Language.el => CountryCode.GR,
            Language.es => CountryCode.ES,
            Language.es_419 => CountryCode.MX,
            Language.fi => CountryCode.FI,
            Language.fil => CountryCode.PH,
            Language.fr => CountryCode.FR,
            Language.hr_hr => CountryCode.HR,
            Language.hu => CountryCode.HU,
            Language.id => CountryCode.ID,
            Language.it => CountryCode.IT,
            Language.ja => CountryCode.JP,
            Language.ko => CountryCode.KR,
            Language.lt => CountryCode.LT,
            Language.lv_lv => CountryCode.LV,
            Language.ms_my => CountryCode.MY,
            Language.nl => CountryCode.NL,
            Language.no => CountryCode.NO,
            Language.pl => CountryCode.PL,
            Language.pt => CountryCode.PT,
            Language.pt_br => CountryCode.BR,
            Language.ro => CountryCode.RO,
            Language.ru => CountryCode.RU,
            Language.sk => CountryCode.SK,
            Language.sl => CountryCode.SI,
            Language.sr => CountryCode.RS,
            Language.sv => CountryCode.SE,
            Language.th => CountryCode.TH,
            Language.tr => CountryCode.TR,
            Language.uk => CountryCode.UA,
            Language.vi => CountryCode.VN,
            Language.zh => CountryCode.CN,
            Language.zh_hant => CountryCode.TW,
            _ => CountryCode.Unknown,
        };
    }
}

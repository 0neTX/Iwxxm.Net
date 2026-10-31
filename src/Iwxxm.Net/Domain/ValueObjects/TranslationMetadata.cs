using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object holding the TAC-to-IWXXM translation metadata of a report (<c>common.xsd</c> <c>ReportType</c>
    /// attributes <c>translatedBulletinID</c>, <c>translatedBulletinReceptionTime</c>, <c>translationCentreDesignator</c>,
    /// <c>translationCentreName</c> and <c>translationTime</c>). Every member is optional (<c>null</c> when the attribute is
    /// absent); times are in UTC. The untranslatable TAC itself is <see cref="IwxxmReport.TranslationFailedTac"/>.
    /// Equality is structural and exact.
    /// </summary>
    public sealed class TranslationMetadata : IEquatable<TranslationMetadata>
    {
        public TranslationMetadata(
            string? translatedBulletinId,
            DateTimeOffset? translatedBulletinReceptionTimeUtc,
            string? translationCentreDesignator,
            string? translationCentreName,
            DateTimeOffset? translationTimeUtc)
        {
            TranslatedBulletinId = translatedBulletinId;
            TranslatedBulletinReceptionTimeUtc = translatedBulletinReceptionTimeUtc;
            TranslationCentreDesignator = translationCentreDesignator;
            TranslationCentreName = translationCentreName;
            TranslationTimeUtc = translationTimeUtc;
        }

        /// <summary>Identifier of the TAC bulletin that was translated (<c>translatedBulletinID</c>, e.g. <c>WSYU31 YUSO 101000</c>).</summary>
        public string? TranslatedBulletinId { get; }

        /// <summary>Time at which the translation centre received the TAC bulletin (<c>translatedBulletinReceptionTime</c>).</summary>
        public DateTimeOffset? TranslatedBulletinReceptionTimeUtc { get; }

        /// <summary>ICAO location indicator of the translation centre (<c>translationCentreDesignator</c>).</summary>
        public string? TranslationCentreDesignator { get; }

        /// <summary>Name of the translation centre (<c>translationCentreName</c>).</summary>
        public string? TranslationCentreName { get; }

        /// <summary>Time at which the translation was performed (<c>translationTime</c>).</summary>
        public DateTimeOffset? TranslationTimeUtc { get; }

        public bool Equals(TranslationMetadata? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(TranslatedBulletinId, other.TranslatedBulletinId, StringComparison.Ordinal) &&
                   Nullable.Equals(TranslatedBulletinReceptionTimeUtc, other.TranslatedBulletinReceptionTimeUtc) &&
                   string.Equals(TranslationCentreDesignator, other.TranslationCentreDesignator, StringComparison.Ordinal) &&
                   string.Equals(TranslationCentreName, other.TranslationCentreName, StringComparison.Ordinal) &&
                   Nullable.Equals(TranslationTimeUtc, other.TranslationTimeUtc);
        }

        public override bool Equals(object? obj) => obj is TranslationMetadata other && Equals(other);

        public override int GetHashCode()
        {
            int hash = ValueEquality.Add(ValueEquality.Seed, TranslatedBulletinId);
            hash = ValueEquality.Add(hash, TranslatedBulletinReceptionTimeUtc);
            hash = ValueEquality.Add(hash, TranslationCentreDesignator);
            hash = ValueEquality.Add(hash, TranslationCentreName);
            return ValueEquality.Add(hash, TranslationTimeUtc);
        }

        public static bool operator ==(TranslationMetadata? left, TranslationMetadata? right) => Equals(left, right);

        public static bool operator !=(TranslationMetadata? left, TranslationMetadata? right) => !Equals(left, right);
    }
}

namespace Qylent.Kutuphane.Core.Domain;

public sealed record ProfileFieldTemplate(
    string Key,
    string Name,
    MemberFieldType FieldType,
    bool IsSensitive = false,
    string? ChoiceOptionsJson = null);

public static class LibraryProfileRules
{
    public const string LegacyClassKey = "profile.core.class";
    public const string LegacyUnitKey = "profile.core.unit";

    public static string DisplayName(LibraryType type) => type switch
    {
        LibraryType.School => "Okul kütüphanesi",
        LibraryType.Public => "Halk kütüphanesi",
        LibraryType.PrivateInstitution => "Özel kurum kütüphanesi",
        _ => "Genel kütüphane"
    };

    public static string Description(LibraryType type) => type switch
    {
        LibraryType.School => "Üyelerde Sınıf, Veli adı ve Veli telefonu kullanılır.",
        LibraryType.Public => "Üyelerde Doğum tarihi ve Üyelik türü kullanılır.",
        LibraryType.PrivateInstitution => "Üyelerde Birim ve Sicil numarası kullanılır.",
        _ => "Sade genel üye kaydı kullanılır; ek alanlar yönetimden tanımlanabilir."
    };

    public static string ClassOrUnitLabel(LibraryType type) => type switch
    {
        LibraryType.School => "Sınıf",
        LibraryType.PrivateInstitution => "Birim",
        _ => "Sınıf / birim"
    };

    public static IReadOnlyList<ProfileFieldTemplate> Fields(LibraryType type) => type switch
    {
        LibraryType.School =>
        [
            new("profile.school.guardian-name", "Veli adı", MemberFieldType.Text, true),
            new("profile.school.guardian-phone", "Veli telefonu", MemberFieldType.Text, true)
        ],
        LibraryType.Public =>
        [
            new("profile.public.birth-date", "Doğum tarihi", MemberFieldType.Date, true),
            new("profile.public.membership-type", "Üyelik türü", MemberFieldType.Choice, false, "[\"Standart\",\"Çocuk\",\"Öğrenci\"]")
        ],
        LibraryType.PrivateInstitution =>
        [
            new("profile.private.employee-number", "Sicil numarası", MemberFieldType.Text, true)
        ],
        _ => []
    };

    public static IReadOnlyList<ProfileFieldTemplate> AllFields { get; } =
        Enum.GetValues<LibraryType>().SelectMany(Fields).DistinctBy(x => x.Key).ToArray();
}

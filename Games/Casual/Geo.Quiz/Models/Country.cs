namespace Geo.Quiz.Models;

public record Country(
    string Name,       // 영문 국명
    string KorName,    // 한국어 국명
    string Capital,    // 영문 수도명
    string KorCapital, // 한국어 수도명
    string Continent,  // Asia / Europe / Africa / Americas / Oceania
    string FlagEmoji   // 국기 이모지
);

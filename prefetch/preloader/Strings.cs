using System.Globalization;

namespace OmsiLaunch.Modules.Prefetch;

// The preloader's visible text: Brazilian Portuguese, or English for any
// other user interface language.
internal static class Strings
{
    private static readonly bool Portuguese = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt";

    private static string T(string pt, string en) => Portuguese ? pt : en;

    public static string Product => "OMSI Prefetch";
    public static string FirstOpenTitle => T("Pré-carregamento sendo inicializado", "Preloading is being set up");
    public static string FirstOpenText(string map) => T(
        "O mapa “" + map + "” foi solicitado pela primeira vez. O módulo de pré-carregamento está lendo o mapa e trazendo para a memória o máximo que puder antes de abrir o OMSI. Nesta primeira sessão ele também aprende o que o jogo usa; as próximas aberturas serão mais rápidas.",
        "The map “" + map + "” was requested for the first time. The preloading module is reading the map and bringing as much as it can into memory before OMSI opens. In this first session it also learns what the game uses; the next starts will be faster.");
    public static string Preparing(string held) => T("Preparando: " + held + " na memória", "Preparing: " + held + " in memory");
    public static string Ready => T("Pronto. Abrindo o OMSI…", "Ready. Opening OMSI…");

    public static string MonitorTitle(string held, string budget, int percent) => T("Pré-carregamento", "Preloading") + " - " + held + " / " + budget + " (" + percent.ToString(CultureInfo.InvariantCulture) + "%)";
    public static string Phase(string phase) => phase switch
    {
        "survey" => T("lendo o mapa", "reading the map"),
        "prepare" => T("carregando antes do OMSI", "loading before OMSI"),
        "session" => T("acompanhando a sessão", "following the session"),
        "stopping" => T("encerrando", "stopping"),
        _ => phase
    };
    public static string Summary1(string held, string budget, int percent, string phase) => T("Na memória: " + held + " de " + budget + " (" + percent + "%) - " + phase, "In memory: " + held + " of " + budget + " (" + percent + "%) - " + phase);
    public static string Summary2(int used, string usedBytes, int unused, string unusedBytes, int outside, string outsideBytes) => T(
        "Em uso: " + used + " arquivos, " + usedBytes + "   |   sem uso até agora: " + unused + " (" + unusedBytes + ")   |   usados fora da memória: " + outside + " (" + outsideBytes + ")",
        "In use: " + used + " files, " + usedBytes + "   |   not used yet: " + unused + " (" + unusedBytes + ")   |   used outside memory: " + outside + " (" + outsideBytes + ")");
    public static string Summary3(string free, string total, uint load) => T("Memória do sistema: " + free + " livres de " + total + " (" + load + "% em uso)", "System memory: " + free + " free of " + total + " (" + load + "% in use)");
    public static string Summary4(string learned, string map, string tiles, string ai, string user) => T(
        "Aprendidos " + learned + "   |   mapa " + map + "   |   tiles " + tiles + "   |   IA " + ai + "   |   veículos do usuário " + user,
        "Learned " + learned + "   |   map " + map + "   |   tiles " + tiles + "   |   AI " + ai + "   |   user vehicles " + user);
    public static string Summary5(string center, int computed, int total, string feed) => T(
        "Posição estimada: " + center + "   |   tiles indexados: " + computed + " de " + total + "   |   OMSI: " + feed,
        "Estimated position: " + center + "   |   tiles indexed: " + computed + " of " + total + "   |   OMSI: " + feed);
    public static string Connected => T("conectado", "connected");
    public static string Waiting => T("aguardando", "waiting");
    public static string Unknown => T("desconhecida", "unknown");
    public static string Pinned => T("Fixar no topo", "Always on top");
    public static string[] Filters => Portuguese
        ? new[] { "Mostrar: todos", "Mostrar: em uso", "Mostrar: na memória sem uso", "Mostrar: usados fora da memória" }
        : new[] { "Show: all", "Show: in use", "Show: in memory, unused", "Show: used outside memory" };
    public static string[] Columns => Portuguese ? new[] { "Estado", "MB", "Origem", "Usado em", "Arquivo" } : new[] { "State", "MB", "Source", "Used at", "File" };
    public static string State(Holding holding) => holding.State switch
    {
        HoldState.Held => holding.Used ? T("Em uso", "In use") : T("Na memória", "In memory"),
        HoldState.Outside => T("Usado, fora da memória", "Used, outside memory"),
        _ => holding.Written ? T("Liberado (gravado pelo jogo)", "Released (written by the game)") : T("Liberado", "Released")
    };
    public static string Tier(Tier tier) => tier switch
    {
        Prefetch.Tier.Learned => T("aprendido", "learned"),
        Prefetch.Tier.Map => T("mapa", "map"),
        Prefetch.Tier.Tiles => "tiles",
        Prefetch.Tier.AiVehicles => T("IA", "AI"),
        _ => T("usuário", "user")
    };
}

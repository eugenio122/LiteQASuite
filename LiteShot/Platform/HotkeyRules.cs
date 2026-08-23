using System.Text;
using System.Windows.Input;

namespace LiteShot.Platform;

/// <summary>
/// O vocabulário do atalho global: como escrever uma combinação de teclas para o
/// usuário ler, e quais combinações são aceitáveis.
///
/// Reúne o que no código antigo estava espalhado dentro do formulário de opções
/// (<c>GetLocalizedKeyName</c>, <c>ObterNomeAtalhoAtual</c> e a validação inline
/// de teclas reservadas), agora fora da interface e testável isolado.
/// </summary>
public static class HotkeyRules
{
    /// <summary>
    /// Teclas que o Windows nomeia mal ou de forma inconsistente entre layouts.
    /// A tabela veio do código antigo, onde foi montada na tentativa e erro com
    /// o teclado ABNT2.
    /// </summary>
    private static readonly Dictionary<uint, string> KeyNameOverrides = new()
    {
        [0x2C] = "PrintScreen",
        [193] = "/",
        [191] = ";",
        [186] = "Ç",
        [188] = ",",
        [190] = ".",
        [194] = ".",
        [187] = "=",
        [189] = "-",
        [226] = "\\",
    };

    /// <summary>
    /// Teclas que não podem virar o atalho global.
    ///
    /// No código antigo o motivo era técnico: elas eram registradas como hotkeys
    /// globais enquanto o overlay estava aberto, e colidiriam. Agora o overlay usa
    /// KeyBinding local, então a colisão técnica sumiu — mas a restrição continua
    /// por bom senso: um disparo global em Ctrl+C sequestraria o copiar do sistema
    /// inteiro.
    /// </summary>
    private static readonly HashSet<uint> ReservedKeys = new()
    {
        0x41, // A  — selecionar o monitor atual
        0x43, // C  — copiar
        0x53, // S  — salvar
        0x5A, // Z  — desfazer
        0x59, // Y  — refazer
        0x1B, // Esc — cancelar
        0xBB, // = (OemPlus)   — aumentar espessura
        0x6B, // + (Add)
        0xBD, // - (OemMinus)  — diminuir espessura
        0x6D, // - (Subtract)
    };

    /// <summary>
    /// Teclas que podem ser o atalho sozinhas, sem Ctrl/Alt/Shift: PrintScreen,
    /// Pause, Insert, Scroll Lock e F1–F24. Qualquer outra exige modificador —
    /// senão o usuário registraria a letra "A" e ela pararia de funcionar em todo
    /// o sistema. O código antigo não tinha essa proteção.
    /// </summary>
    public static bool CanStandAlone(uint virtualKey) =>
        virtualKey is 0x2C     // PrintScreen
                   or 0x13     // Pause
                   or 0x2D     // Insert
                   or 0x91     // Scroll Lock
        || virtualKey is >= 0x70 and <= 0x87; // F1..F24

    /// <summary>Se a tecla está na lista de reservadas.</summary>
    public static bool IsReserved(uint virtualKey) => ReservedKeys.Contains(virtualKey);

    /// <summary>
    /// Converte os modificadores do WPF nos sinalizadores MOD_* que o
    /// <c>RegisterHotKey</c> espera.
    /// </summary>
    public static uint ToModifierFlags(ModifierKeys modifiers)
    {
        uint flags = NativeMethods.MOD_NONE;

        if (modifiers.HasFlag(ModifierKeys.Alt)) flags |= NativeMethods.MOD_ALT;
        if (modifiers.HasFlag(ModifierKeys.Control)) flags |= NativeMethods.MOD_CONTROL;
        if (modifiers.HasFlag(ModifierKeys.Shift)) flags |= NativeMethods.MOD_SHIFT;
        if (modifiers.HasFlag(ModifierKeys.Windows)) flags |= NativeMethods.MOD_WIN;

        return flags;
    }

    /// <summary>
    /// Texto da combinação inteira, como aparece na caixinha de atalho.
    /// Ex.: <c>"Ctrl + Shift + PrintScreen"</c>.
    /// </summary>
    public static string Describe(uint modifier, uint virtualKey)
    {
        var text = new StringBuilder();

        if ((modifier & NativeMethods.MOD_CONTROL) != 0) text.Append("Ctrl + ");
        if ((modifier & NativeMethods.MOD_SHIFT) != 0) text.Append("Shift + ");
        if ((modifier & NativeMethods.MOD_ALT) != 0) text.Append("Alt + ");
        if ((modifier & NativeMethods.MOD_WIN) != 0) text.Append("Win + ");

        text.Append(KeyName(virtualKey));
        return text.ToString();
    }

    /// <summary>
    /// Nome de uma tecla isolada. Consulta primeiro a tabela de exceções, depois
    /// pergunta ao Windows no layout ativo.
    /// </summary>
    public static string KeyName(uint virtualKey)
    {
        if (virtualKey == 0)
            return string.Empty;

        if (KeyNameOverrides.TryGetValue(virtualKey, out var known))
            return known;

        var scanCode = NativeMethods.MapVirtualKey(virtualKey, 0);
        var lParam = (int)(scanCode << 16);

        // Bit 24 marca as teclas estendidas (setas, Insert/Delete, Home/End,
        // PageUp/PageDown), sem o qual o Windows devolve o nome do numérico.
        if (virtualKey is >= 33 and <= 46)
            lParam |= 0x0100_0000;

        unsafe
        {
            const int bufferSize = 64;
            char* buffer = stackalloc char[bufferSize];

            var length = NativeMethods.GetKeyNameText(lParam, buffer, bufferSize);
            if (length > 0)
                return new string(buffer, 0, length);
        }

        return $"Key {virtualKey}";
    }
}
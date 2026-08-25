using System;
using System.Runtime.InteropServices;

namespace LiteFlow.Platform;

/// <summary>
/// Manda uma pasta para a Lixeira do Windows em vez de apagá-la de vez.
///
/// <b>Por que a Lixeira e não <c>Directory.Delete</c>.</b> Um cenário pode ser meio
/// dia de trabalho — prints, notas e o relatório exportado, tudo na mesma pasta.
/// Um clique errado na árvore não pode ser irreversível.
///
/// <b>Por que P/Invoke.</b> O .NET não tem API gerenciada para a Lixeira;
/// <c>SHFileOperation</c> é o caminho do Windows para isso, e é o mesmo que o
/// Explorer usa.
///
/// <b>Duas armadilhas desta struct, ambas já pagas aqui:</b>
/// <list type="number">
/// <item><b>Nada de <c>Pack</c>.</b> A struct nativa usa o alinhamento natural da
/// plataforma. Forçar <c>Pack = 1</c> encolhe o layout de 56 para 50 bytes em x64 e
/// desalinha os ponteiros: o shell passa a ler <c>pFrom</c> de um endereço que é
/// metade de um ponteiro e metade de outro, sai varrendo memória alheia, e o
/// processo morre com <c>ExecutionEngineException</c> — não com um código de erro.
/// O sintoma não parece marshalling, parece bug do Windows.</item>
/// <item><b>O caminho termina em dois zeros.</b> A API espera uma lista de
/// caminhos; só o segundo terminador diz que a lista acabou.</item>
/// </list>
/// </summary>
public static class RecycleBin
{
    private const uint FO_DELETE = 0x0003;

    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;

    /// <summary>
    /// Layout idêntico ao <c>SHFILEOPSTRUCTW</c> do <c>shellapi.h</c>. Sequencial e
    /// com o alinhamento padrão da plataforma — ver a observação sobre <c>Pack</c>
    /// na documentação da classe.
    ///
    /// <c>fAnyOperationsAborted</c> é declarado como <c>int</c>, e não <c>bool</c>,
    /// porque o campo nativo é um <c>BOOL</c> de quatro bytes e é o shell quem o
    /// escreve de volta: com o tipo exato, não há conversão para dar errado.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        public int fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT fileOperation);

    /// <summary>
    /// Envia a pasta (com tudo dentro) para a Lixeira.
    ///
    /// A confirmação com o usuário é responsabilidade de quem chama —
    /// <c>FOF_NOCONFIRMATION</c> está ligado justamente para não haver duas
    /// perguntas para a mesma decisão. O diálogo de <b>erro</b> do Windows continua
    /// ligado de propósito: "o arquivo está aberto no Word" é uma explicação que
    /// nenhum código de retorno dá tão bem.
    /// </summary>
    /// <param name="folderPath">A pasta a excluir.</param>
    /// <param name="errorCode">
    /// O retorno do shell: 0 é sucesso. Serve para a tela dizer algo mais útil que
    /// "não deu" quando a exclusão falha.
    /// </param>
    /// <param name="owner">
    /// Janela dona do diálogo de erro. Sem ela o diálogo pode aparecer atrás do
    /// aplicativo e parecer que nada aconteceu.
    /// </param>
    public static bool TryDeleteFolder(string folderPath, out int errorCode, IntPtr owner = default)
    {
        errorCode = 0;
        if (string.IsNullOrWhiteSpace(folderPath)) return false;

        var operation = new SHFILEOPSTRUCT
        {
            hwnd = owner,
            wFunc = FO_DELETE,

            // O terminador duplo é obrigatório: sem ele a API sai lendo memória
            // adiante procurando o fim da lista.
            pFrom = folderPath + '\0' + '\0',
            pTo = null,

            // Sem FOF_NOCONFIRMATION o Windows perguntaria de novo. A contrapartida
            // conhecida: se a pasta não couber na Lixeira, ele exclui direto em vez
            // de perguntar. Para um cenário (dezenas ou poucas centenas de MB) isso
            // não acontece com a Lixeira em tamanho padrão.
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT,

            fAnyOperationsAborted = 0,
            hNameMappings = IntPtr.Zero,
            lpszProgressTitle = null
        };

        errorCode = SHFileOperation(ref operation);

        return errorCode == 0 && operation.fAnyOperationsAborted == 0;
    }
}
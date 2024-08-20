using Core;


string inputDirectory = @"C:\\Projetos\\Apps\\public_repos\\RepositorioGitHub\\DotNet\\HowToRabbitMQ\\ConsoleApp1\\imagens";
string outputDirectory = @"C:\\Projetos\\Apps\\public_repos\\RepositorioGitHub\\DotNet\\HowToRabbitMQ\\ConsoleApp1\\imagens_processadas";

// Define os nomes dos filtros
string[] filters = { "grayscale", "negative", "sepia", "blur" };


// Verifica se a pasta de entrada existe
if (!Directory.Exists(inputDirectory))
{
    Console.WriteLine($"Pasta de entrada '{inputDirectory}' não encontrada.");
    return;
}

// Cria a pasta de saída se ela não existir
if (!Directory.Exists(outputDirectory))
{
    Directory.CreateDirectory(outputDirectory);
}

// Obtém todos os arquivos de imagem na pasta de entrada
var imageFiles = Directory.GetFiles(inputDirectory);

if (imageFiles.Length == 0)
{
    Console.WriteLine("Nenhuma imagem encontrada na pasta 'imagens'.");
    return;
}

// Instancia o serviço de filtro
var filterService = new FilterService();

// Aplica todos os filtros a cada imagem na pasta de entrada
foreach (var imagePath in imageFiles)
{
    foreach (var filter in filters)
    {
        string imageName = Path.GetFileNameWithoutExtension(imagePath);
        string extension = Path.GetExtension(imagePath);

        // Define o caminho da imagem de saída
        string outputImagePath = Path.Combine(outputDirectory, $"{imageName}_{filter}{extension}");

        try
        {
            // Aplica o filtro
            await filterService.FilterHandler(imagePath, filter, outputImagePath);
            Console.WriteLine($"Imagem '{imageName}{extension}' processada com filtro '{filter}' e salva em '{outputImagePath}'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao processar a imagem '{imageName}{extension}' com filtro '{filter}': {ex.Message}");
        }
    }
}

Console.WriteLine("Processamento concluído.");
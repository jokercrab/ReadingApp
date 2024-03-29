﻿using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using General.DataModels;
using WuxiaClassLib.DataModels;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http.Headers;
using Scraper;
using System.Xml;
using Microsoft.Extensions.Configuration;
using System.Xml.Linq;

namespace WuxiaApp.Servs;
public enum picParams
{
    preview,
    bigpic,
    source
}

public class BaseServices
{

    static protected HttpClient client;
    /// <summary>
    /// auxiliary collection for forming image path
    /// </summary>
    readonly Dictionary<picParams, string> ImageParams;
    readonly string api;
    readonly string hostSite;
    readonly PreferenceServices preference;
    Dictionary<string, Book> books = new Dictionary<string, Book>();

    WuxiaScraper scraper;


    public BaseServices(){
        
     }
    public BaseServices(IConfiguration conf,PreferenceServices preference)
    {
        this.preference = preference;
        api = conf.GetRequiredSection("api").Value;
        hostSite = conf.GetRequiredSection("host").Value;
        client = new HttpClient() { BaseAddress = new Uri(api) };
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        scraper = new WuxiaScraper();
        books = new Dictionary<string, Book>();
        ImageParams = new()
        {
            [picParams.preview] = "_cover.jpg?width=400&quality=80",
            [picParams.bigpic] = "_cover.jpg",
            [picParams.source] = conf.GetRequiredSection("images").Value

        };

    }


    /// <summary>
    /// Gets library data and user preferences stored in local file
    /// </summary>
    /// <param name="fileSystem"></param>
    /// <returns>Dictionary that contains book objects, where key is an books name slug</returns>
    public async Task<Dictionary<string, Book>> GetBooksLocalAsync(IFileSystem fileSystem)
    {

        if (books?.Count > 0)
            return books;

        var path = Path.Combine(fileSystem.AppDataDirectory, "library.dat");
        if (!File.Exists(path))
            return books;
        var contents = await File.ReadAllTextAsync(path);
        books = JsonSerializer.Deserialize<Dictionary<string, Book>>(contents);
        
        return books;
    }

    /// <summary>
    /// Perfoms a search for book with specified parameters
    /// </summary>
    /// <param name="searchPattern">Pattern for searching within books name</param>
    /// <param name="ordering">Books order</param>
    /// <param name="category">Book category</param>
    /// <param name="limit">Max range of books loaded</param>
    /// <returns></returns>
    public async Task<searchResult> SearchBookAsync(string searchPattern = "", string ordering = "-total_views", string category = "", string limit = "10")
    {
        var query = new Dictionary<string, string>()
        {
            ["search"] = searchPattern,
            ["limit"] = limit,
            ["order"] = ordering,
            ["category_name"] = category
        };
        var querysrting = QueryHelpers.AddQueryString("search/", query);
        using var request = new HttpRequestMessage(HttpMethod.Get, querysrting);
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();

        var jsonObject = await response.Content.ReadFromJsonAsync<searchResult>();


        return jsonObject;
    }
    /// <summary>
    /// Not in use currently. Use <see cref="SearchBookAsync"/> instead
    /// </summary>
    /// <param name="categories"></param>
    /// <param name="ordering"></param>
    /// <returns></returns>
    public async Task<searchResult> AdvancedFilteringAsync(string categories = "", string ordering = "-total_views")
    {

        var query = new Dictionary<string, string>()
        {
            ["itemsPerPage"] = "10",
            ["category_include"] = categories,
            ["order"] = ordering
        };
        var querysrting = QueryHelpers.AddQueryString("novel-filter/", query);
        using var request = new HttpRequestMessage(HttpMethod.Get, querysrting);
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<searchResult>();
    }
    /// <summary>
    /// Copies specified file from AppPackege to AppDataDir
    /// </summary>
    /// <param name="sourceFile">File name inside AppPackage</param>
    /// <param name="targetFileName">File name inside AppDataDir</param>
    /// <returns></returns>
    public async Task CopyLibraryFileAsync(string sourceFile, string targetFileName)
    {
        using Stream fileStream = await FileSystem.Current.OpenAppPackageFileAsync(sourceFile);
        using StreamReader reader = new(fileStream);

        string content = await reader.ReadToEndAsync();

        string targetFile = Path.Combine(FileSystem.Current.AppDataDirectory, targetFileName);

        using FileStream outputStream = File.OpenWrite(targetFile);
        using StreamWriter streamWriter = new(outputStream);

        await streamWriter.WriteAsync(content);
    }

    /// <summary>
    /// Deletes book from current collection
    /// </summary>
    /// <param name="book">Book object to be deleted</param>
    /// <exception cref="ArgumentException"></exception>
    public void DeleteBook(Book book)
    {
        var result = books.Remove(book.Slug);
        if (!result)
            throw new ArgumentException("Book is not present in the lib collection", nameof(book));
    }
    /// <summary>
    /// Saves current user data to local files
    /// </summary>
    /// <param name="fileSystem">Current file system</param>
    public void Save(IFileSystem fileSystem)
    {
        var content = JsonSerializer.Serialize(books);
        try
        {
            using (var stream = File.Open(
            Path.Combine(fileSystem.AppDataDirectory, "library.dat"),
            FileMode.Create))
            {
                using var writer = new StreamWriter(stream);
                writer.WriteLine(content);
            }
            preference.Save();
        }
        catch (Exception ex)
        {
            Shell.Current.DisplayAlert("Error!", $"{ex.Message}'\nWhile attempting to save data", "Ok");
            Debug.WriteLine(ex.Message);
        }
    }
    /// <summary>
    /// Adds a new book to the library
    /// </summary>
    /// <param name="book"> Book object to be added</param>
    /// <exception cref="ArgumentException"></exception>
    public void AddNewBook(Book book)
    {
        if (books.ContainsKey(book.Slug))
            throw new ArgumentException($"Error book {book.Title} by {book.Author.name} is already present is the library");
        books.Add(book.Slug, book);

    }
    /// <summary>
    /// Gets deatailed info of the specified book
    /// </summary>
    /// <param name="name">Slug name of the book</param>
    /// <returns></returns>
    public async Task<BookInfo> GetBookInfoAsync(string name)
    {
        BookInfo result = null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "novels/" + name);
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            result = await response.Content.ReadFromJsonAsync<BookInfo>();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"{ex.Message}/nWhile retrieving detailed book info", "Ok");
            Debug.WriteLine($"{ex.Message}");
        }

        return result;

    }
    /// <summary>
    ///  Forms path for picture
    ///  
    /// </summary>
    /// <param name="slugName">slug name of the book</param>
    /// <param name="picParam">picture quality parametr(preview or bigpic)</param>
    /// <returns> A string representing uri path for picture</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public string FormPicturePath(string originalImage,string novelSlug ,picParams sizeParam = picParams.preview)
    {
        ArgumentNullException.ThrowIfNull(novelSlug);

        if (originalImage == null)
            return "unloaded_image.png";
        //return ImageParams[picParams.source] + slugName + ImageParams[sizeParam];   This domain is not supported for now!!
        return originalImage;
        
    }
    /// <summary>
    /// Checks if specific book is present in the lib
    /// </summary>
    /// <param name="book">Book to check</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool CheckBookInLib(Book book)
    {
        ArgumentNullException.ThrowIfNull(book);
        return books.ContainsKey(book.Slug);
    }
    /// <summary>
    /// Loads nex data chunk from specified query
    /// </summary>
    /// <param name="path">Query returned from previous search</param>
    /// <returns></returns>
    public async Task<searchResult> LoadNextDataAsync(Uri path)
    {
        searchResult jsonObject = null;
        try
        {
            using var response = await client.GetAsync("search/" + path.Query);

            response.EnsureSuccessStatusCode();

            jsonObject = await response.Content.ReadFromJsonAsync<searchResult>();

        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Oops!", "Error occurred while loading new data.\n Pls try again later", "Ok");
            Debug.WriteLine($"{ex.Message}\n in {nameof(LoadNextDataAsync)}");
        }



        return jsonObject;
    }
    /// <summary>
    /// Retrieves chapter text
    /// </summary>
    /// <param name="chapSlug">Slug name of chapter</param>
    /// <returns></returns>
    public async Task<ChapterData> FetchChapterAsync(string chapSlug)
    {
        scraper.SiteUri = new Uri(hostSite + "chapter/" + chapSlug);
        return await scraper.GetScriptContentDOMAsync<ChapterData>();
    }
    /// <summary>
    /// Returns specific book's object currently stored in memory
    /// </summary>
    /// <param name="book"></param>
    /// <returns></returns>
    public Book GetLocalBookData(Book book)
    {
        ArgumentNullException.ThrowIfNull(book, nameof(book));
        return books[book.Slug];
    }

}


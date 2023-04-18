﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using General.DataModels;
using System.Diagnostics;
using WuxiaApp.Servs;
using WuxiaClassLib.DataModels;


namespace WuxiaApp.ViewModels;
//[QueryProperty(nameof(Name),"Book")]
public partial class DetailsViewModel : BaseViewModel, IQueryAttributable
{
    readonly Services services;
    readonly LibraryViewModel libraryViewModel;

    [ObservableProperty]
    Book book;
    [ObservableProperty]
    string addButtonText;
    [ObservableProperty]
    bool addButtonBehavior = true;
    public DetailsViewModel(Services services,LibraryViewModel libVm)
    {
        libraryViewModel = libVm;
        this.services = services;
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (IsBusy)
            return;

        if (query["slug"] is not string slug)
            return;
        await GetBookDataAsync(slug);
    }

    async Task GetBookDataAsync(string name)
    {
        try
        {

            IsBusy = true;
            var bookinfo = await services.GetBookInfoAsync(name);

            var book = new Book()
            {
                Chapters = bookinfo.chapters,
                Description = bookinfo.description,
                Ratings = bookinfo.rating,
                Title = bookinfo.name,
                Views = bookinfo.human_views,
                Author = bookinfo.author,
                Categories = bookinfo.category,
                FirstChapter = bookinfo.first_chapter,
                LastUpdate = bookinfo.last_chap_updated,
                Status = bookinfo.novelStatus,
                Ranking = bookinfo.ranking
            };
            if (bookinfo.image == null)
                book.PicturePath = "unloaded_image.png";
            else
                book.PicturePath = services.FormPicturePath(bookinfo.slug,"bigpic");
            
            Book = book;
            if (services.CheckBookInLib(book))
            {
                AddButtonText = "Already in your library";
                AddButtonBehavior = false;
            }
            else
                AddButtonText = "Add to your library";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unable to get books: {ex.Message}");
            await Shell.Current.DisplayAlert("Error!", ex.Message, "OK");

        }
        { IsBusy = false; }

    }
    [RelayCommand]
    void AddNewBook(Book book)
    {
        services.AddNewBook(book);
        libraryViewModel.Books.Add(book);
    }


}



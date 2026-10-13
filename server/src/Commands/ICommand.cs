using WTelegram.Types;

namespace Cinegram.Commands;

public interface ICommand
{
    Task Execute(string[] args, Message msg);
    
    string Key { get; }
    string Description { get; }
    string Usage { get; }
}
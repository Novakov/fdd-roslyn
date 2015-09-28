using Commands;

namespace CommandHandlers
{
    public abstract class CommandHandler<TCommand>
        where TCommand : Command
    {
        public abstract void Execute(TCommand command);
    }
}
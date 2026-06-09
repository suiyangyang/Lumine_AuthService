using MediatR;

namespace Lumine.AuthServer.Application.Commands
{
    public record AssignRoleToUserCommand(Guid UserId, Guid RoleId) : IRequest<bool>;
}

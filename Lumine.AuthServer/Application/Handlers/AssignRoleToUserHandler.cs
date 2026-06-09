using Lumine.AuthServer.Domain.Abstractions;
using Lumine.AuthServer.Infrastructure;
using MediatR;

namespace Lumine.AuthServer.Application.Handlers
{
    public class AssignRoleToUserHandler : IRequestHandler<Commands.AssignRoleToUserCommand, bool>
    {
        private readonly IUserRepository _userRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly AuthDbContext _db;

        public AssignRoleToUserHandler(IUserRepository userRepository, IRoleRepository roleRepository, AuthDbContext db)
        {
            _userRepository = userRepository;
            _roleRepository = roleRepository;
            _db = db;
        }

        public async Task<bool> Handle(Commands.AssignRoleToUserCommand request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId);
            if (user == null) return false;
            var role = await _roleRepository.GetByIdAsync(request.RoleId);
            if (role == null) return false;

            user.AssignRole(role);
            _db.Update(user);
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}

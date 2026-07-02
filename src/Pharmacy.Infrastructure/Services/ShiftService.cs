using System;
using System.Threading.Tasks;
using Pharmacy.Core.DTOs;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Exceptions;
using Pharmacy.Core.Interfaces;

namespace Pharmacy.Infrastructure.Services
{
    public class ShiftService : IShiftService
    {
        private readonly IShiftRepository _shiftRepository;
        private readonly IUserRepository _userRepository;

        public ShiftService(IShiftRepository shiftRepository, IUserRepository userRepository)
        {
            _shiftRepository = shiftRepository;
            _userRepository = userRepository;
        }

        public async Task<ShiftDto> OpenShiftAsync(Guid userId, decimal startingCash)
        {
            var activeShift = await _shiftRepository.GetActiveShiftAsync();
            if (activeShift != null)
            {
                throw new ShiftAlreadyOpenException($"Một ca làm việc đang được mở bởi nhân viên {activeShift.User?.FullName}. Vui lòng chốt ca hiện tại trước.");
            }

            if (startingCash < 0)
            {
                throw new PharmacyValidationException("Tiền mặt đầu ca không thể nhỏ hơn 0.");
            }

            var shift = new Shift
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                StartTime = DateTime.UtcNow,
                StartingCash = startingCash,
                Status = "Open"
            };

            await _shiftRepository.AddAsync(shift);
            await _shiftRepository.SaveChangesAsync();

            // Load user info for response DTO
            var user = await _userRepository.GetByIdAsync(userId);

            return new ShiftDto
            {
                Id = shift.Id,
                UserId = shift.UserId,
                UserFullName = user?.FullName,
                StartTime = shift.StartTime,
                StartingCash = shift.StartingCash,
                ActualRevenue = shift.ActualRevenue,
                Status = shift.Status
            };
        }

        public async Task<ShiftDto> CloseShiftAsync(decimal endingCash)
        {
            var activeShift = await _shiftRepository.GetActiveShiftAsync();
            if (activeShift == null)
            {
                throw new ShiftClosedException("Không có ca làm việc nào đang mở để đóng.");
            }

            if (endingCash < 0)
            {
                throw new PharmacyValidationException("Tiền mặt chốt ca thực tế không thể nhỏ hơn 0.");
            }

            activeShift.EndTime = DateTime.UtcNow;
            activeShift.EndingCash = endingCash;
            activeShift.Status = "Closed";

            await _shiftRepository.SaveChangesAsync();

            return new ShiftDto
            {
                Id = activeShift.Id,
                UserId = activeShift.UserId,
                UserFullName = activeShift.User?.FullName,
                StartTime = activeShift.StartTime,
                EndTime = activeShift.EndTime,
                StartingCash = activeShift.StartingCash,
                EndingCash = activeShift.EndingCash,
                ActualRevenue = activeShift.ActualRevenue,
                Status = activeShift.Status
            };
        }

        public async Task<ShiftDto?> GetActiveShiftAsync()
        {
            var activeShift = await _shiftRepository.GetActiveShiftAsync();
            if (activeShift == null) return null;

            return new ShiftDto
            {
                Id = activeShift.Id,
                UserId = activeShift.UserId,
                UserFullName = activeShift.User?.FullName,
                StartTime = activeShift.StartTime,
                StartingCash = activeShift.StartingCash,
                ActualRevenue = activeShift.ActualRevenue,
                Status = activeShift.Status
            };
        }
    }
}

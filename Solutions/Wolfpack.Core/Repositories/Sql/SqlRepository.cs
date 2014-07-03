﻿using System;
using System.Linq;
using Wolfpack.Core.Interfaces;
using Wolfpack.Core.Interfaces.Entities;

namespace Wolfpack.Core.Repositories.Sql
{
    public class SqlRepository : INotificationRepository
    {
        private readonly ISqlDbContext _context;

        public SqlRepository()
            : this(new SqlDbContext("Wolfpackv3"))
        {
                
        }

        public SqlRepository(ISqlDbContext context)
        {
            _context = context;
        }

        public void Initialise()
        {
            //throw new NotImplementedException();
        }

        public IQueryable<NotificationEvent> Filter(params INotificationRepositoryQuery[] filters)
        {
            return filters.Aggregate(_context.Notifications.AsQueryable(), (current, filter) => filter.Filter(current));            
        }

        public bool GetById(Guid id, out NotificationEvent notification)
        {
            notification = _context.Notifications.FirstOrDefault(n => n.Id.Equals(id));
            return notification != null;
        }

        public IQueryable<NotificationEvent> GetByState(MessageStateTypes state)
        {
            return _context.Notifications.Where(n => n.State.Equals(state));
        }

        public void Add(NotificationEvent notification)
        {
            _context.Notifications.Add(notification);
            _context.SaveChanges();
        }

        public void Delete(Guid notificationId)
        {
            var notification = _context.Notifications.FirstOrDefault(n => n.Id.Equals(notificationId));

            if (notification == null)
                return;

            _context.Notifications.Remove(notification);
            _context.SaveChanges();
        }
    }
}
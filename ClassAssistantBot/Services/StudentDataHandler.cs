﻿using System;
using System.Text;
using ClassAssistantBot.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassAssistantBot.Services
{
    public class StudentDataHandler
    {
        private DataAccess dataAccess { get; set; }

        public StudentDataHandler(DataAccess dataAccess)
        {
            this.dataAccess = dataAccess;
        }

        public string RemoveStudentFromClassRoom(long id, string userName)
        {
            var teacher = dataAccess.Users.First(x => x.Id == id);
            var user = dataAccess.Users.FirstOrDefault(x => x.Username == userName.Substring(1));

            if (user == null)
                return "No existe extudiante con ese nombre de usuario, por favor atienda lo que hace.";
            else
            {
                teacher.Status = UserStatus.Ready;
                var userByClassRoom = dataAccess.StudentsByClassRooms.Where(x => x.Student.UserId == user.Id && teacher.ClassRoomActiveId == x.ClassRoomId);
                user.ClassRoomActiveId = 0;
                dataAccess.Users.Update(user);
                dataAccess.StudentsByClassRooms.RemoveRange(userByClassRoom);
                dataAccess.Users.Update(teacher);
                dataAccess.SaveChanges();
                return "Estudiante sacado del aula con éxito";
            }
        }

        public string RemoveStudentFromClassRoom(long id)
        {
            var user = dataAccess.Users.Where(x => x.Id == id).First();
            user.Status = UserStatus.RemoveStudentFromClassRoom;
            dataAccess.Update(user);
            dataAccess.SaveChanges();
            var list = dataAccess.StudentsByClassRooms
                .Where(x => x.ClassRoomId == user.ClassRoomActiveId)
                .Include(x => x.Student.User).ToList();
            var res = new StringBuilder();
            foreach (var item in list)
            {
                res.Append(item.Student.User.FirstName);
                res.Append(" ");
                res.Append(item.Student.User.LastName);
                res.Append(": @");
                res.Append(item.Student.User.Username);
                res.Append("\n");
            }
            return res.ToString();
        }

        public void StudentEnterClass(User user)
        {
            user.Status = UserStatus.StudentEnteringClass;
            dataAccess.Users.Update(user);
            dataAccess.SaveChanges();
            Console.WriteLine($"The student {user.Username} is entering class");
        }

        public string AssignStudentAtClass(long id, string codeText)
        {
            int code = 0;
            bool canParse = int.TryParse(codeText, out code);

            if (!canParse)
                return $"No hay aula creada con el código de acceso {codeText}";

            var user = dataAccess.Users.Where(x => x.TelegramId == id).FirstOrDefault();
            var classRoom = dataAccess.ClassRooms.Where(x => x.StudentAccessKey == code).FirstOrDefault();
            if (classRoom == null)
                return $"No hay aula creada con el código de acceso {code}";
            else
            {
                var student = dataAccess.Students.Where(x => x.UserId == user.Id).FirstOrDefault();

                if (student == null)
                {
                    student = new Student
                    {
                        UserId = user.Id,
                        StudentsClassRooms = new List<StudentByClassRoom>(),
                        Id = Guid.NewGuid().ToString()
                    };
                }

                var studentByClassRoom = new StudentByClassRoom
                {
                    ClassRoomId = classRoom.Id,
                    Id = Guid.NewGuid().ToString(),
                    Student = student
                };

                user.Status = UserStatus.Ready;
                user.ClassRoomActiveId = classRoom.Id;

                dataAccess.Users.Update(user);
                dataAccess.StudentsByClassRooms.Add(studentByClassRoom);
                dataAccess.SaveChanges();
                Console.WriteLine($"The teacher {user.Username} has entered class");
                return $"Ha entrado en el aula satiscatoriamente";
            }
        }

        public string GetStudentsOnClassByTeacherId(long id)
        {
            var teacher = dataAccess.Teachers.Where(x => x.UserId == id).Include(x => x.User).First();
            var classRoom = dataAccess.ClassRooms.Where(x => x.Id == teacher.User.ClassRoomActiveId).First();
            var students = dataAccess.StudentsByClassRooms
                .Where(x => x.ClassRoomId == teacher.User.ClassRoomActiveId)
                .Include(x => x.ClassRoom)
                .Include(x => x.Student)
                .ThenInclude(x => x.User)
                .ToList();

            var res = new StringBuilder($"Estudantes inscritos en el aula {classRoom.Name}:\n");
            for (int i = 0; i < students.Count; i++)
            {
                res.Append(i + 1);
                res.Append(": @");
                res.Append(students[i].Student.User.Username);
                res.Append(" -> ");
                var credit = dataAccess.Credits.Where(x => x.UserId == students[i].Student.UserId && x.ClassRoomId == students[i].Student.User.ClassRoomActiveId).Sum(x => x.Value);
                res.Append(credit);
                res.Append("\n");
            }
            return res.ToString();
        }
    }
}

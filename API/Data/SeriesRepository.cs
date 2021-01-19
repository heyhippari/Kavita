﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data
{
    public class SeriesRepository : ISeriesRepository
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;

        public SeriesRepository(DataContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public void Add(Series series)
        {
            _context.Series.Add(series);
        }

        public void Update(Series series)
        {
            _context.Entry(series).State = EntityState.Modified;
        }

        public async Task<bool> SaveAllAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }
        
        public bool SaveAll()
        {
            return _context.SaveChanges() > 0;
        }

        public async Task<Series> GetSeriesByNameAsync(string name)
        {
            return await _context.Series.SingleOrDefaultAsync(x => x.Name == name);
        }
        
        public Series GetSeriesByName(string name)
        {
            return _context.Series.SingleOrDefault(x => x.Name == name);
        }
        
        public async Task<IEnumerable<Series>> GetSeriesForLibraryIdAsync(int libraryId)
        {
            return await _context.Series
                .Where(s => s.LibraryId == libraryId)
                .OrderBy(s => s.SortName)
                .ToListAsync();
        }
        
        public async Task<IEnumerable<SeriesDto>> GetSeriesDtoForLibraryIdAsync(int libraryId, int userId)
        {
            var sw = Stopwatch.StartNew();
            var series = await _context.Series
                .Where(s => s.LibraryId == libraryId)
                .OrderBy(s => s.SortName)
                .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
            
            
            await AddSeriesModifiers(userId, series);


            Console.WriteLine("Processed GetSeriesDtoForLibraryIdAsync in {0} milliseconds", sw.ElapsedMilliseconds);
            return series;
        }

        public async Task<IEnumerable<VolumeDto>> GetVolumesDtoAsync(int seriesId, int userId)
        {
            var volumes =  await _context.Volume
                .Where(vol => vol.SeriesId == seriesId)
                .OrderBy(volume => volume.Number)
                .ProjectTo<VolumeDto>(_mapper.ConfigurationProvider)
                .AsNoTracking()
                .ToListAsync();
            
            await AddVolumeModifiers(userId, volumes);

            return volumes;

        }


        public IEnumerable<Volume> GetVolumes(int seriesId)
        {
            return _context.Volume
                .Where(vol => vol.SeriesId == seriesId)
                .Include(vol => vol.Files)
                .OrderBy(vol => vol.Number)
                .ToList();
        }

        public async Task<SeriesDto> GetSeriesDtoByIdAsync(int seriesId, int userId)
        {
            var series = await _context.Series.Where(x => x.Id == seriesId)
                .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
                .SingleAsync();

            var seriesList = new List<SeriesDto>() {series};
            await AddSeriesModifiers(userId, seriesList);
            
            return seriesList[0];
        }

        public async Task<Volume> GetVolumeAsync(int volumeId)
        {
            return await _context.Volume
                .Include(vol => vol.Files)
                .SingleOrDefaultAsync(vol => vol.Id == volumeId);
        }

        public async Task<VolumeDto> GetVolumeDtoAsync(int volumeId, int userId)
        {
            var volume = await _context.Volume
                .Where(vol => vol.Id == volumeId)
                .Include(vol => vol.Files)
                .ProjectTo<VolumeDto>(_mapper.ConfigurationProvider)
                .SingleAsync(vol => vol.Id == volumeId);

            var volumeList = new List<VolumeDto>() {volume};
            await AddVolumeModifiers(userId, volumeList);

            return volumeList[0];
        }

        /// <summary>
        /// Returns all volumes that contain a seriesId in passed array.
        /// </summary>
        /// <param name="seriesIds"></param>
        /// <returns></returns>
        public async Task<IEnumerable<Volume>> GetVolumesForSeriesAsync(int[] seriesIds)
        {
            return await _context.Volume
                .Where(v => seriesIds.Contains(v.SeriesId))
                .ToListAsync();
        }

        public async Task<bool> DeleteSeriesAsync(int seriesId)
        {
            var series = await _context.Series.Where(s => s.Id == seriesId).SingleOrDefaultAsync();
            _context.Series.Remove(series);
            
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<Volume> GetVolumeByIdAsync(int volumeId)
        {
            return await _context.Volume.SingleOrDefaultAsync(x => x.Id == volumeId);
        }
        
        private async Task AddSeriesModifiers(int userId, List<SeriesDto> series)
        {
            var userProgress = await _context.AppUserProgresses
                .Where(p => p.AppUserId == userId && series.Select(s => s.Id).Contains(p.SeriesId))
                .ToListAsync();

            var userRatings = await _context.AppUserRating
                .Where(r => r.AppUserId == userId && series.Select(s => s.Id).Contains(r.SeriesId))
                .ToListAsync();

            foreach (var s in series)
            {
                s.PagesRead = userProgress.Where(p => p.SeriesId == s.Id).Sum(p => p.PagesRead);
                var rating = userRatings.SingleOrDefault(r => r.SeriesId == s.Id);
                if (rating == null) continue;
                s.UserRating = rating.Rating;
                s.UserReview = rating.Review;
            }
        }
        private async Task AddVolumeModifiers(int userId, List<VolumeDto> volumes)
        {
            var userProgress = await _context.AppUserProgresses
                .Where(p => p.AppUserId == userId && volumes.Select(s => s.Id).Contains(p.VolumeId))
                .AsNoTracking()
                .ToListAsync();

            foreach (var v in volumes)
            {
                v.PagesRead = userProgress.Where(p => p.VolumeId == v.Id).Sum(p => p.PagesRead);
            }
        }
    }
}
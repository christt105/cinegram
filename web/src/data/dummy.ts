import { ref } from 'vue';

export type MediaType = 'Movie' | 'Series';

export interface DummyMedia {
  id: string;
  name: string;
  type: MediaType;
  year: number;
  tags: string[];
  image: string;
  quality: string;
  duration?: string;
  synopsis?: string;
}

export const useDummyData = () => {
  const items = ref<DummyMedia[]>([
    {
      id: '1',
      name: 'Dune: Part Two',
      type: 'Movie',
      year: 2024,
      tags: ['Sci-Fi', 'Adventure'],
      image: 'https://image.tmdb.org/t/p/w600_and_h900_bestv2/1pdfLvkbY9ohJlCjQH2JGjjc91p.jpg',
      quality: '4K HDR',
      duration: '2h 46m',
      synopsis: 'Paul Atreides unites with Chani and the Fremen while on a warpath of revenge against the conspirators who destroyed his family.'
    },
    {
      id: '2',
      name: 'The Boys',
      type: 'Series',
      year: 2019,
      tags: ['Action', 'Comedy'],
      image: 'https://image.tmdb.org/t/p/w600_and_h900_bestv2/7Ns6tOqsN0jE3wNswiO2K5Q7y79.jpg',
      quality: '1080p',
      synopsis: 'A group of vigilantes set out to take down corrupt superheroes who abuse their superpowers.'
    },
    {
      id: '3',
      name: 'Interstellar',
      type: 'Movie',
      year: 2014,
      tags: ['Sci-Fi', 'Drama'],
      image: 'https://image.tmdb.org/t/p/w600_and_h900_bestv2/gEU2QniE6E77NI6lCU6MvrIdlsR.jpg',
      quality: '4K REMUX',
      duration: '2h 49m',
      synopsis: 'A team of explorers travel through a wormhole in space in an attempt to ensure humanity\'s survival.'
    },
    {
      id: '4',
      name: 'Spider-Man: Across the Spider-Verse',
      type: 'Movie',
      year: 2023,
      tags: ['Animation', 'Action'],
      image: 'https://image.tmdb.org/t/p/w600_and_h900_bestv2/8Vt6mWEReuy4Of61Lnj5Xj704m8.jpg',
      quality: '1080p',
      duration: '2h 20m',
      synopsis: 'Miles Morales catapults across the Multiverse, where he encounters a team of Spider-People charged with protecting its very existence.'
    },
    {
      id: '5',
      name: 'Fallout',
      type: 'Series',
      year: 2024,
      tags: ['Sci-Fi', 'Action'],
      image: 'https://image.tmdb.org/t/p/w600_and_h900_bestv2/8zHjPd94YJ87Z8YqA7eK4QYv4p0.jpg',
      quality: '4K WEB-DL',
      synopsis: 'In a future, post-apocalyptic Los Angeles brought about by nuclear decimation, citizens must live in underground bunkers to protect themselves from radiation, mutants and bandits.'
    },
    {
      id: '6',
      name: 'Oppenheimer',
      type: 'Movie',
      year: 2023,
      tags: ['History', 'Drama'],
      image: 'https://image.tmdb.org/t/p/w600_and_h900_bestv2/8Gxv8gSFCU0XGDykEGv7zR1n2ua.jpg',
      quality: '4K',
      duration: '3h 0m',
      synopsis: 'The story of J. Robert Oppenheimer\'s role in the development of the atomic bomb during World War II.'
    }
  ]);

  return { items };
};

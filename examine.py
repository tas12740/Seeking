from json import load
from sys import argv
import numpy as np

arr = []

with open('FindN.txt', 'wt') as fp:
    fp.write('')

for i in range(1, int(argv[1])+1):
    with open(f'Experiment{i}.json', 'rt') as fp:
        curr_json = load(fp)
    arr.extend(curr_json['tileResults'])

    curr_bfs = curr_json['bfsTimes']
    curr_dfs = curr_json['dfsTimes']
    curr_tile = curr_json['tileTimes']

    with open('FindN.txt', 'at') as fp:
        fp.write('Experiment {}:\n\n'.format(i))

    for t in range(5, 101, 5):
        bfs = np.array(curr_bfs)[:t]
        
        dfs = np.array(curr_dfs)[:t]

        tiles = np.array(curr_tile)[:t]

        with open('FindN.txt', 'at') as fp:
            fp.write('Number: {}\n'.format(t))
            fp.write('Mean of BFS: {:.2f} \t Standard Deviation of BFS: {:.2f}\n'.format(np.mean(bfs), np.std(bfs)))
            fp.write('Mean of DFS: {:.2f} \t Standard Deviation of DFS: {:.2f}\n'.format(np.mean(dfs), np.std(dfs)))
            fp.write('Mean of Tile: {:.2f} \t Standard Deviation of Tile: {:.2f}\n'.format(np.mean(tiles), np.std(tiles)))
            fp.write('\n')

print ('Number of successes: {}'.format(sum(arr)))
print ('Total: {}'.format(len(arr)))
